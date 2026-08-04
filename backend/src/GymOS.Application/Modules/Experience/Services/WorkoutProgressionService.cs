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
    Task ApplyWorkoutAsync(Guid memberId, Guid workoutLogId, CancellationToken cancellationToken);
}

public class WorkoutProgressionService(IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IWorkoutProgressionService
{
    public async Task ApplyWorkoutAsync(Guid memberId, Guid workoutLogId, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? await db.Members.IgnoreQueryFilters().Where(m => m.Id == memberId).Select(m => (Guid?)m.TenantId)
                .FirstOrDefaultAsync(cancellationToken);
        if (tenantId is null)
        {
            return;
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
            return;
        }

        foreach (var exerciseId in log.Entries.Select(e => e.ExerciseId).Distinct())
        {
            var sessionEntries = log.Entries
                .Where(e => e.ExerciseId == exerciseId)
                .Select(e => (e.SetsCompleted, e.RepsCompleted, e.WeightKg))
                .ToList();
            var session = PersonalRecordPolicy.StatsFor(sessionEntries);

            await AppendPersonalRecordsAsync(tenantId.Value, memberId, exerciseId, session, workoutLogId, log.LoggedAt, cancellationToken);
            await RecomputeMasteryAsync(tenantId.Value, memberId, exerciseId, log.LoggedAt, cancellationToken);
        }
    }

    private async Task AppendPersonalRecordsAsync(
        Guid tenantId, Guid memberId, Guid exerciseId, ExerciseSessionStats session, Guid workoutLogId, DateTimeOffset achievedAt,
        CancellationToken cancellationToken)
    {
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
            }
        }
    }

    private async Task RecomputeMasteryAsync(
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

        var mastery = await db.ExerciseMasteries.FirstOrDefaultAsync(
            m => m.MemberId == memberId && m.ExerciseId == exerciseId, cancellationToken);
        if (mastery is null)
        {
            mastery = new ExerciseMastery { TenantId = tenantId, MemberId = memberId, ExerciseId = exerciseId };
            db.ExerciseMasteries.Add(mastery);
        }

        mastery.Sessions = history.Select(h => h.WorkoutLogId).Distinct().Count();
        mastery.TotalSets = history.Sum(h => h.SetsCompleted);
        mastery.TotalReps = history.Sum(h => (long)h.RepsCompleted);
        mastery.TotalVolume = history.Sum(h => h.SetsCompleted * h.RepsCompleted * (h.WeightKg ?? 0));
        mastery.BestWeightKg = history.Max(h => h.WeightKg ?? 0);
        mastery.BestEstimatedOneRepMax = history.Max(h => OneRepMax.Epley(h.WeightKg ?? 0, h.RepsCompleted));
        mastery.LastTrainedAt = history.Count > 0 ? history.Max(h => h.LoggedAt) : fallbackLastTrained;
        mastery.UpdatedAt = dateTimeProvider.UtcNow;
    }
}
