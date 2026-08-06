using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Experience.Dtos;
using GymOS.Application.Modules.Portal;
using GymOS.Domain.Common;
using GymOS.Domain.Experience;
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

        var restDates = (await db.RecoveryLogs.AsNoTracking()
                .Where(r => r.MemberId == memberId).Select(r => r.LoggedOn).ToListAsync(cancellationToken))
            .Distinct().ToList();

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
            var groupByExercise = (await db.Exercises.AsNoTracking()
                    .Where(x => exerciseIds.Contains(x.Id) && x.MuscleGroup != null)
                    .Select(x => new { x.Id, x.MuscleGroup })
                    .ToListAsync(cancellationToken))
                .ToDictionary(x => x.Id, x => x.MuscleGroup!);

            muscleGroups = entryRows
                .Where(r => groupByExercise.ContainsKey(r.ExerciseId))
                .Select(r => new { Group = groupByExercise[r.ExerciseId], Date = GymDay.Of(r.LoggedAt, zone) })
                .GroupBy(x => x.Group)
                .Select(g =>
                {
                    var dates = g.Select(x => x.Date).Distinct().ToList();
                    var times7 = dates.Count(d => d >= windowStart);
                    var daysSince = today.DayNumber - dates.Max().DayNumber;
                    var (status, reason) = RecoveryPolicy.ClassifyMuscleGroup(
                        new RecoveryPolicy.MuscleRecoverySignals(g.Key, times7, daysSince));
                    return new MuscleRecoveryDto(g.Key, status.ToString(), reason, times7, daysSince);
                })
                .OrderByDescending(m => m.TimesLast7Days).ThenBy(m => m.MuscleGroup)
                .ToList();
        }

        return new MyRecoveryDto(overallStatus.ToString(), overallReason, sessionsLast7, restLast7, daysSinceLastWorkout, muscleGroups);
    }
}
