using GymOS.Application.Common.Interfaces;
using GymOS.Application.Modules.Experience.Services;
using GymOS.Domain.Experience;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Challenges.Services;

/// <summary>
/// The single place challenge completion gets evaluated — shared by both triggers: logging a new
/// workout (EvaluateChallengeProgressOnWorkoutLoggedHandler) and joining a challenge the member
/// already qualifies for from workouts logged before they joined (JoinChallengeCommand). Without this
/// shared check, joining a challenge whose target your recent history already clears would sit
/// "in progress" forever, since nothing would ever re-trigger the count — a real gap caught during
/// Slice 8 live verification.
/// </summary>
public interface IChallengeProgressService
{
    /// <summary>Re-checks every one of a member's active (not yet completed) participations and
    /// completes + awards XP for any that now meet their target.</summary>
    Task EvaluateAsync(Guid memberId, CancellationToken cancellationToken);
}

public class ChallengeProgressService(IApplicationDbContext db, IMemberXpService xp, IDateTimeProvider dateTimeProvider)
    : IChallengeProgressService
{
    public async Task EvaluateAsync(Guid memberId, CancellationToken cancellationToken)
    {
        var activeParticipations = await db.ChallengeParticipants
            .Where(p => p.MemberId == memberId && !p.IsCompleted)
            .ToListAsync(cancellationToken);
        if (activeParticipations.Count == 0)
        {
            return;
        }

        var challengeIds = activeParticipations.Select(p => p.ChallengeId).Distinct().ToList();
        var challenges = await db.CommunityChallenges.AsNoTracking()
            .Where(c => challengeIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        // Pull the member's workout dates once; DateTimeOffset can't be range-filtered against a
        // DateOnly window in SQL on SQLite, so reduce to dates in memory (same pattern as Recovery).
        var workoutDates = (await db.WorkoutLogs.AsNoTracking()
                .Where(w => w.MemberId == memberId)
                .Select(w => w.LoggedAt)
                .ToListAsync(cancellationToken))
            .Select(d => DateOnly.FromDateTime(d.UtcDateTime))
            .ToList();

        var now = dateTimeProvider.UtcNow;

        foreach (var participation in activeParticipations)
        {
            if (!challenges.TryGetValue(participation.ChallengeId, out var challenge))
            {
                continue; // challenge deleted since the member joined
            }

            var count = workoutDates.Count(d => d >= challenge.StartDate && d <= challenge.EndDate);
            if (count < challenge.TargetWorkoutCount)
            {
                continue;
            }

            participation.IsCompleted = true;
            participation.CompletedAt = now;
            await xp.AwardAsync(memberId, XpReason.ChallengeCompleted, XpSourceType.Challenge, participation.Id, cancellationToken);
        }
    }
}
