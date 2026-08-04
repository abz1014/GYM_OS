using GymOS.Application.Common.Interfaces;
using GymOS.Domain.Experience;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Experience.Services;

/// <summary>
/// Evaluates the achievement catalog for a member and unlocks anything newly earned. Runs in the
/// second dispatch pass (off MemberProgressionChangedEvent), so every stat it reads — workouts,
/// visits, level, PRs, mastery — is already committed from the first pass. Idempotent: it only
/// inserts codes the member doesn't already have, so re-evaluation never duplicates a badge. Mutates
/// the tracked context only; GymOsDbContext persists at the end of the dispatch pass.
/// </summary>
public interface IAchievementService
{
    Task EvaluateAsync(Guid memberId, CancellationToken cancellationToken);
}

public class AchievementService(IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IAchievementService
{
    public async Task EvaluateAsync(Guid memberId, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? await db.Members.IgnoreQueryFilters().Where(m => m.Id == memberId).Select(m => (Guid?)m.TenantId)
                .FirstOrDefaultAsync(cancellationToken);
        if (tenantId is null)
        {
            return;
        }

        var stats = await BuildStatsAsync(memberId, cancellationToken);

        var alreadyUnlocked = (await db.MemberAchievements.AsNoTracking()
            .Where(a => a.MemberId == memberId)
            .Select(a => a.Code)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var now = dateTimeProvider.UtcNow;

        foreach (var code in AchievementCatalog.EarnedCodes(stats))
        {
            if (alreadyUnlocked.Contains(code))
            {
                continue;
            }

            db.MemberAchievements.Add(new MemberAchievement
            {
                TenantId = tenantId.Value,
                MemberId = memberId,
                Code = code,
                UnlockedAt = now
            });
            alreadyUnlocked.Add(code); // guard against the same code being earned twice in one pass
        }
    }

    private async Task<AchievementStats> BuildStatsAsync(Guid memberId, CancellationToken cancellationToken)
    {
        var workoutCount = await db.WorkoutLogs.CountAsync(w => w.MemberId == memberId, cancellationToken);
        var visitCount = await db.AttendanceRecords.CountAsync(a => a.MemberId == memberId, cancellationToken);
        var personalRecordCount = await db.PersonalRecords.CountAsync(r => r.MemberId == memberId, cancellationToken);
        var level = await db.MemberProgressions.AsNoTracking()
            .Where(p => p.MemberId == memberId).Select(p => (int?)p.Level).FirstOrDefaultAsync(cancellationToken) ?? 1;

        // Distinct muscle groups trained — two simple queries rather than a join, to stay clear of the
        // SQLite translation limits the in-memory test provider imposes.
        var trainedExerciseIds = await db.ExerciseMasteries.AsNoTracking()
            .Where(m => m.MemberId == memberId).Select(m => m.ExerciseId).ToListAsync(cancellationToken);
        var muscleGroups = await db.Exercises.AsNoTracking()
            .Where(e => trainedExerciseIds.Contains(e.Id) && e.MuscleGroup != null)
            .Select(e => e.MuscleGroup)
            .Distinct()
            .ToListAsync(cancellationToken);

        return new AchievementStats(workoutCount, visitCount, level, personalRecordCount, muscleGroups.Count);
    }
}
