using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Experience.Dtos;
using GymOS.Application.Modules.Portal;
using GymOS.Domain.Common;
using GymOS.Domain.Experience;
using GymOS.Domain.Workouts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Experience.Queries;

/// <summary>
/// The member's recovery snapshot — overall status + a per-muscle-group breakdown — computed by the
/// pure <see cref="RecoveryPolicy"/> from the trailing week of logged workouts and rest days. A read
/// model with no stored projection: recovery is a function of recent history, so it is recomputed
/// every request (and is rebuildable by construction). Self-scoped via MyMemberResolver.
/// </summary>
public record GetMyRecoveryQuery : IQuery<MyRecoveryDto>;

public class GetMyRecoveryQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetMyRecoveryQuery, MyRecoveryDto>
{
    public async Task<MyRecoveryDto> Handle(GetMyRecoveryQuery request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);
        // On the gym's clock, like every other day the member is shown. Recovery is the first thing
        // the home screen says, so counting it in UTC put the headline on a different calendar from
        // the weekly ring directly above it.
        var zone = await MyMemberResolver.ResolveGymZoneAsync(db, memberId, cancellationToken);
        var today = GymDay.Of(dateTimeProvider.UtcNow, zone);
        var windowStart = today.AddDays(-6); // 7-day inclusive window (today .. today-6)

        // Whole-body signals: distinct workout days (an entry-less log still counts as a gym session)
        // and logged rest days. DateTimeOffset can't be aggregated in SQL on SQLite, so reduce to dates
        // in memory.
        var workoutDates = (await db.WorkoutLogs.AsNoTracking()
                .Where(w => w.MemberId == memberId).Select(w => w.LoggedAt).ToListAsync(cancellationToken))
            .Select(d => GymDay.Of(d, zone)).Distinct().ToList();

        // Kind and Notes come along for the ride rather than in a second round trip: the same rows
        // answer both "how many rest days this week" and "what did I log today", and one query that
        // returns three columns beats two queries over the same table.
        var recoveryRows = await db.RecoveryLogs.AsNoTracking()
            .Where(r => r.MemberId == memberId)
            .Select(r => new { r.LoggedOn, r.Kind, r.Notes })
            .ToListAsync(cancellationToken);

        var restDates = recoveryRows.Select(r => r.LoggedOn).Distinct().ToList();

        // Only one log per day can exist — LogMyRecoveryCommand returns the existing row instead of
        // adding a second — so this is a lookup, not a pick-the-latest.
        var todayRow = recoveryRows.FirstOrDefault(r => r.LoggedOn == today);
        var todayLog = todayRow is null ? null : new MyRecoveryTodayDto(todayRow.Kind.ToString(), todayRow.Notes);

        var sessionsLast7 = workoutDates.Count(d => d >= windowStart);
        var restLast7 = restDates.Count(d => d >= windowStart);
        int? daysSinceLastWorkout = workoutDates.Count == 0 ? null : today.DayNumber - workoutDates.Max().DayNumber;

        var (overallStatus, overallReason) = RecoveryPolicy.ClassifyOverall(
            new RecoveryPolicy.RecoverySignals(sessionsLast7, restLast7, daysSinceLastWorkout));

        // Per-muscle-group: pull the member's log entries (join up to the log for its date), map each to
        // its exercise's muscle group, then reduce per group in memory. Querying WorkoutLogEntries
        // directly (not SelectMany over a navigation) keeps SQLite from needing an APPLY.
        var entryRows = await db.WorkoutLogEntries.AsNoTracking()
            .Where(e => e.WorkoutLog!.MemberId == memberId)
            .Select(e => new { e.ExerciseId, e.WorkoutLog!.LoggedAt })
            .ToListAsync(cancellationToken);

        var muscleGroups = new List<MuscleRecoveryDto>();
        if (entryRows.Count > 0)
        {
            var exerciseIds = entryRows.Select(r => r.ExerciseId).Distinct().ToList();

            /*
             * EVERY group a movement works, not just the one it is filed under.
             *
             * This read used Exercise.MuscleGroup — one label per movement — and so believed a
             * deadlift trained only the back. The morning after heavy deadlifts the screen told the
             * member their legs were "fully rested — a good target for your next session", which is
             * the app making a claim about somebody's body that their body could contradict. Same
             * class of defect as the fabricated rep count, and more directly wrong: acting on it
             * means training a fatigued muscle.
             *
             * Secondary work counts as WORK here and nowhere else. For "has this been trained
             * recently" the honest answer for a deadlift-worked back is yes; for anything measuring
             * HOW MUCH, counting it would need an intensity model the app does not have. See
             * ExerciseMuscle for the full line.
             */
            var musclesByExercise = (await db.ExerciseMuscles.AsNoTracking()
                    .Where(m => exerciseIds.Contains(m.ExerciseId))
                    .Select(m => new { m.ExerciseId, m.MuscleGroupKey, m.Role })
                    .ToListAsync(cancellationToken))
                .GroupBy(m => m.ExerciseId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Movements with no muscle rows yet — created by a gym before this table existed, or by
            // the create form, which writes the primary only — still report their single group, so a
            // catalogue that has not been backfilled degrades to exactly the old behaviour rather
            // than to silence.
            var groupByExercise = (await db.Exercises.AsNoTracking()
                    .Where(x => exerciseIds.Contains(x.Id) && x.MuscleGroup != null)
                    .Select(x => new { x.Id, x.MuscleGroup })
                    .ToListAsync(cancellationToken))
                .ToDictionary(x => x.Id, x => x.MuscleGroup!);

            /*
             * Grouped through MuscleGroupVocabulary, not on the raw string.
             *
             * Exercise.MuscleGroup is free text a gym owner types, and this query used it as a key
             * directly. The body map on the Train screen then matched those keys against its own
             * hard-coded zone names, so the two only agreed because the seeder happens to write
             * exactly "Chest"/"Back"/"Legs". A gym that types "Quads" produced a fatigued group the
             * map could not shade and a leg that stayed rested-looking through a leg day — the
             * screen quietly showing the opposite of the truth.
             *
             * Resolving here rather than in the client is what makes that impossible: the DTO now
             * carries the canonical key, so every consumer is grouping the same way by construction.
             * The DISPLAY name is the vocabulary's too, so "Quads" and "quadriceps" both read "Legs"
             * instead of two rows for one leg.
             */
            // One row per (group, day, role) the entry touches. A session with a squat AND a deadlift
            // hits legs twice on one day, which is why the dates are DISTINCTed below — "times in the
            // last 7 days" has always meant days trained, not movements performed.
            muscleGroups = entryRows
                .SelectMany(r =>
                {
                    var date = GymDay.Of(r.LoggedAt, zone);

                    if (musclesByExercise.TryGetValue(r.ExerciseId, out var muscles))
                    {
                        return muscles.Select(m => new
                        {
                            Group = MuscleGroupVocabulary.All.FirstOrDefault(v => v.Key == m.MuscleGroupKey)
                                    ?? MuscleGroupVocabulary.Other,
                            Date = date,
                            Primary = m.Role == MuscleRole.Primary
                        });
                    }

                    return groupByExercise.TryGetValue(r.ExerciseId, out var label)
                        ? [new { Group = MuscleGroupVocabulary.Resolve(label), Date = date, Primary = true }]
                        : Enumerable.Empty<dynamic>().Select(_ =>
                            new { Group = MuscleGroupVocabulary.Other, Date = date, Primary = true });
                })
                .GroupBy(x => x.Group)
                .Select(g =>
                {
                    var dates = g.Select(x => x.Date).Distinct().ToList();
                    var times7 = dates.Count(d => d >= windowStart);
                    var lastDay = dates.Max();
                    var daysSince = today.DayNumber - lastDay.DayNumber;

                    /*
                     * Whether the MOST RECENT work on this group targeted it directly.
                     *
                     * Scoped to the last day rather than to the member's whole history, because the
                     * question the sentence answers is "why is this fatigued NOW". Someone who
                     * squats every week has trained their legs directly plenty; if what actually
                     * loaded them yesterday was a deadlift, "trained in the last day" sends them
                     * looking for a leg session they never did.
                     */
                    var directly = g.Where(x => x.Date == lastDay).Any(x => x.Primary);

                    var (status, reason) = RecoveryPolicy.ClassifyMuscleGroup(
                        new RecoveryPolicy.MuscleRecoverySignals(g.Key.DisplayName, times7, daysSince, directly));
                    return new MuscleRecoveryDto(
                        g.Key.DisplayName, g.Key.Key, status.ToString(), reason, times7, daysSince, directly);
                })
                .OrderByDescending(m => m.TimesLast7Days).ThenBy(m => m.MuscleGroup)
                .ToList();
        }

        return new MyRecoveryDto(
            overallStatus.ToString(), overallReason, sessionsLast7, restLast7, daysSinceLastWorkout, muscleGroups, todayLog);
    }
}
