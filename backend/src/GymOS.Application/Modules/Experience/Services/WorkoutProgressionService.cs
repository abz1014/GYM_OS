using GymOS.Application.Common.Interfaces;
using GymOS.Domain.Experience;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Experience.Services;

/// <summary>
/// Applies a logged workout to the two workout-derived projections: appends any personal records it
/// set, and refreshes exercise mastery. Both are idempotent — PRs by the strictly-greater rule,
/// mastery by recomputing from the member's full history for each exercise touched — so a re-published
/// WorkoutLoggedEvent or a projection rebuild never double-counts. Mutates the tracked context only;
/// GymOsDbContext saves when it finishes dispatching (inside the command's transaction).
/// </summary>
public interface IWorkoutProgressionService
{
    /// <returns>True if this workout set at least one personal record, so the caller can award the
    /// improvement XP. Returned rather than awarded here because this service owns projections, not
    /// rewards — the same separation the WorkoutLogged handlers already keep.</returns>
    Task<bool> ApplyWorkoutAsync(Guid memberId, Guid workoutLogId, CancellationToken cancellationToken);

    /// <summary>Recomputes one (member, exercise) mastery row from that exercise's full logged
    /// history — the exact reduction ApplyWorkoutAsync uses per touched exercise, exposed so a full
    /// projection rebuild can reuse it instead of re-deriving the same rule.</summary>
    Task RecomputeMasteryAsync(Guid tenantId, Guid memberId, Guid exerciseId, DateTimeOffset fallbackLastTrained, CancellationToken cancellationToken);
}

public class WorkoutProgressionService(IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IWorkoutProgressionService
{
    public async Task<bool> ApplyWorkoutAsync(Guid memberId, Guid workoutLogId, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? await db.Members.IgnoreQueryFilters().Where(m => m.Id == memberId).Select(m => (Guid?)m.TenantId)
                .FirstOrDefaultAsync(cancellationToken);
        if (tenantId is null)
        {
            return false;
        }

        var log = await db.WorkoutLogs.AsNoTracking()
            .Where(w => w.Id == workoutLogId)
            .Select(w => new
            {
                w.LoggedAt,
                Entries = w.Entries.Select(e => new { e.ExerciseId, e.SetsCompleted, e.RepsCompleted, e.WeightKg }).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (log is null)
        {
            return false;
        }

        var setAnyRecord = false;

        foreach (var exerciseId in log.Entries.Select(e => e.ExerciseId).Distinct())
        {
            var sessionEntries = log.Entries
                .Where(e => e.ExerciseId == exerciseId)
                .Select(e => (e.SetsCompleted, e.RepsCompleted, e.WeightKg))
                .ToList();
            var session = PersonalRecordPolicy.StatsFor(sessionEntries);

            setAnyRecord |= await AppendPersonalRecordsAsync(tenantId.Value, memberId, exerciseId, session, workoutLogId, log.LoggedAt, cancellationToken);
            await RecomputeMasteryAsync(tenantId.Value, memberId, exerciseId, log.LoggedAt, cancellationToken);
        }

        return setAnyRecord;
    }

    /// <returns>True if any record row was written for this exercise.</returns>
    private async Task<bool> AppendPersonalRecordsAsync(
        Guid tenantId, Guid memberId, Guid exerciseId, ExerciseSessionStats session, Guid workoutLogId, DateTimeOffset achievedAt,
        CancellationToken cancellationToken)
    {
        var wrote = false;

        foreach (var type in Enum.GetValues<PersonalRecordType>())
        {
            var candidate = PersonalRecordPolicy.ValueFor(session, type);

            // Prior best includes any row a previous application of THIS same workout wrote, so a
            // re-run matches (never exceeds) it -> idempotent. Max in memory keeps it provider-safe.
            var priorValues = await db.PersonalRecords
                .Where(r => r.MemberId == memberId && r.ExerciseId == exerciseId && r.Type == type)
                .Select(r => r.Value)
                .ToListAsync(cancellationToken);
            var priorBest = priorValues.Count == 0 ? 0m : priorValues.Max();

            if (PersonalRecordPolicy.Beats(candidate, priorBest))
            {
                db.PersonalRecords.Add(new PersonalRecord
                {
                    TenantId = tenantId,
                    MemberId = memberId,
                    ExerciseId = exerciseId,
                    Type = type,
                    Value = candidate,
                    WorkoutLogId = workoutLogId,
                    AchievedAt = achievedAt
                });

                wrote = true;
            }
        }

        return wrote;
    }

    public async Task RecomputeMasteryAsync(
        Guid tenantId, Guid memberId, Guid exerciseId, DateTimeOffset fallbackLastTrained, CancellationToken cancellationToken)
    {
        // Recompute from the member's entire history for this exercise, so the projection equals a
        // from-scratch rebuild and applying the same workout twice is a no-op. Query the entries
        // directly and join up to their log — a SelectMany over the log's Entries navigation would
        // compile to a SQL APPLY, which SQLite (the in-memory test provider) can't translate.
        var history = await db.WorkoutLogEntries.AsNoTracking()
            .Where(e => e.ExerciseId == exerciseId && e.WorkoutLog!.MemberId == memberId)
            .Select(e => new { e.WorkoutLogId, LoggedAt = e.WorkoutLog!.LoggedAt, e.SetsCompleted, e.RepsCompleted, e.WeightKg })
            .ToListAsync(cancellationToken);

        if (history.Count == 0)
        {
            return;
        }

        // .Local before the database, matching MemberXpService/AchievementService. Two workouts for
        // the same member and exercise can land in one SaveChanges pass — a bulk import, a projection
        // rebuild, or seeding — and each raises its own WorkoutLoggedEvent before anything is
        // persisted. A DB-only lookup misses the row the first handler added but hasn't saved, adds a
        // second, and violates the one-row-per-member-per-exercise unique index.
        var mastery = db.ExerciseMasteries.Local.FirstOrDefault(
                m => m.MemberId == memberId && m.ExerciseId == exerciseId)
            ?? await db.ExerciseMasteries.FirstOrDefaultAsync(
                m => m.MemberId == memberId && m.ExerciseId == exerciseId, cancellationToken);
        if (mastery is null)
        {
            mastery = new ExerciseMastery { TenantId = tenantId, MemberId = memberId, ExerciseId = exerciseId };
            db.ExerciseMasteries.Add(mastery);
        }

        mastery.Sessions = history.Select(h => h.WorkoutLogId).Distinct().Count();
        mastery.TotalSets = history.Sum(h => h.SetsCompleted);
        /*
         * An entry with no reps contributes nothing to a rep total or a volume, rather than zero.
         *
         * `?? 0` would compile and would be a different lie: it says a run was zero reps, which then
         * enters a sum as though it had been measured. Skipping is what "there is no rep count here"
         * actually means. TotalVolume already collapsed to zero for these rows via the null weight,
         * so only TotalReps changes value — and it was the fabricated 8 that made it wrong before.
         */
        mastery.TotalReps = history.Sum(h => (long)(h.RepsCompleted ?? 0));
        mastery.TotalVolume = history.Sum(h => h.SetsCompleted * (h.RepsCompleted ?? 0) * (h.WeightKg ?? 0));
        mastery.BestWeightKg = history.Max(h => h.WeightKg ?? 0);
        mastery.BestEstimatedOneRepMax = history.Max(h =>
            h.RepsCompleted is null ? 0m : OneRepMax.Epley(h.WeightKg ?? 0, h.RepsCompleted.Value));
        mastery.LastTrainedAt = history.Count > 0 ? history.Max(h => h.LoggedAt) : fallbackLastTrained;
        mastery.UpdatedAt = dateTimeProvider.UtcNow;
    }
}
