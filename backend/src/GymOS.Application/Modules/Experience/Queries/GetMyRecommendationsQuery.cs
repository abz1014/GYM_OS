using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Experience.Dtos;
using GymOS.Application.Modules.Portal;
using GymOS.Application.Modules.Portal.Queries;
using GymOS.Domain.Experience;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Experience.Queries;

/// <summary>
/// The member's coaching nudges in one round trip — plateau alerts, weekly focus, volume trend,
/// recovery advice, and skill-tree exercise substitution — synthesized by the pure
/// RecommendationPolicy from signals the rest of the Member Experience Engine already computes.
/// Deliberately reuses GetMyWorkoutSuggestionsQuery, GetMyMasteryQuery, and GetMyRecoveryQuery via
/// ISender rather than recomputing their logic (the established pattern — see
/// GetMyWorkoutAssignmentsQuery). Self-scoped via MyMemberResolver.
/// </summary>
public record GetMyRecommendationsQuery : IQuery<List<MyRecommendationDto>>;

public class GetMyRecommendationsQueryHandler(
    IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider, ISender sender)
    : IRequestHandler<GetMyRecommendationsQuery, List<MyRecommendationDto>>
{
    public async Task<List<MyRecommendationDto>> Handle(GetMyRecommendationsQuery request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);
        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);

        var recommendations = new List<Recommendation>();

        // A trainer's active plan decides whether the self-directed "what to train" recommendations
        // (WeeklyFocus, ExerciseSubstitution) run at all — recovery/plateau/volume stay independent,
        // since those are about the member's own body state, not program direction.
        var activePlanName = await db.WorkoutAssignments.AsNoTracking()
            .Where(a => a.MemberId == memberId && a.StartDate <= today && (a.EndDate == null || a.EndDate >= today))
            .Select(a => a.WorkoutTemplate!.Name)
            .FirstOrDefaultAsync(cancellationToken);

        if (activePlanName is not null)
        {
            recommendations.Add(RecommendationPolicy.TrainerPlanActive(activePlanName));
        }

        var recovery = await sender.Send(new GetMyRecoveryQuery(), cancellationToken);
        var recoveryAdvice = RecommendationPolicy.RecoveryAdvice(Enum.Parse<RecoveryStatus>(recovery.Status), recovery.Reason);
        if (recoveryAdvice is not null)
        {
            recommendations.Add(recoveryAdvice);
        }

        var suggestions = await sender.Send(new GetMyWorkoutSuggestionsQuery(), cancellationToken);
        var overloadSignals = suggestions
            .Select(s => new ExerciseOverloadSignal(s.ExerciseId, s.ExerciseName, s.Suggestion, s.LastWeightKg))
            .ToList();
        recommendations.AddRange(RecommendationPolicy.PlateauAlerts(overloadSignals));

        if (activePlanName is null)
        {
            var mastery = await sender.Send(new GetMyMasteryQuery(), cancellationToken);
            var groupSignals = mastery.MuscleGroups.Select(g => new MuscleGroupSignal(g.Name, g.MasteryPercent)).ToList();
            var weeklyFocus = RecommendationPolicy.WeeklyFocus(groupSignals);
            if (weeklyFocus is not null)
            {
                recommendations.Add(weeklyFocus);
            }

            recommendations.AddRange(await BuildExerciseSubstitutionsAsync(memberId, cancellationToken));
        }

        var volumeRecommendation = await BuildVolumeTrendAsync(memberId, today, cancellationToken);
        if (volumeRecommendation is not null)
        {
            recommendations.Add(volumeRecommendation);
        }

        return recommendations.Select(r => new MyRecommendationDto(r.Type.ToString(), r.Title, r.Explanation, r.ExerciseId)).ToList();
    }

    /// <summary>Week-over-week logged training volume. No existing query owns this metric, so it's
    /// computed directly here; pulled to memory since DateTimeOffset can't be bucketed in SQL on SQLite.</summary>
    private async Task<Recommendation?> BuildVolumeTrendAsync(Guid memberId, DateOnly today, CancellationToken cancellationToken)
    {
        var rows = await db.WorkoutLogEntries.AsNoTracking()
            .Where(e => e.WorkoutLog!.MemberId == memberId)
            .Select(e => new { e.WorkoutLog!.LoggedAt, e.SetsCompleted, e.RepsCompleted, e.WeightKg })
            .ToListAsync(cancellationToken);

        var currentWindowStart = today.AddDays(-6);
        var previousWindowStart = today.AddDays(-13);
        var previousWindowEnd = today.AddDays(-7);

        var currentVolume = 0m;
        var previousVolume = 0m;
        foreach (var row in rows)
        {
            var date = DateOnly.FromDateTime(row.LoggedAt.UtcDateTime);
            var volume = row.SetsCompleted * row.RepsCompleted * (row.WeightKg ?? 0m);

            if (date >= currentWindowStart)
            {
                currentVolume += volume;
            }
            else if (date >= previousWindowStart && date <= previousWindowEnd)
            {
                previousVolume += volume;
            }
        }

        return RecommendationPolicy.VolumeTrend(currentVolume, previousVolume);
    }

    /// <summary>
    /// Exercise-substitution recommendations from the tenant's skill trees. SkillNode carries no
    /// TenantId of its own (mirrors WorkoutTemplateExercise) — the tenant boundary is enforced by
    /// resolving this tenant's tree ids through db.SkillTrees first (which DOES carry the global
    /// tenant query filter) and only then querying nodes by that id list.
    /// </summary>
    private async Task<List<Recommendation>> BuildExerciseSubstitutionsAsync(Guid memberId, CancellationToken cancellationToken)
    {
        var treeIds = await db.SkillTrees.AsNoTracking().Select(t => t.Id).ToListAsync(cancellationToken);
        if (treeIds.Count == 0)
        {
            return [];
        }

        var nodes = await db.SkillNodes.AsNoTracking()
            .Where(n => treeIds.Contains(n.SkillTreeId))
            .Select(n => new { n.Id, n.SkillTreeId, n.ExerciseId, n.OrderIndex, n.MinReps, n.UnlockExplanation, ExerciseName = n.Exercise!.Name })
            .ToListAsync(cancellationToken);

        if (nodes.Count == 0)
        {
            return [];
        }

        var exerciseIds = nodes.Select(n => n.ExerciseId).Distinct().ToList();
        var bestReps = (await db.WorkoutLogEntries.AsNoTracking()
                .Where(e => exerciseIds.Contains(e.ExerciseId) && e.WorkoutLog!.MemberId == memberId)
                .Select(e => new { e.ExerciseId, e.RepsCompleted })
                .ToListAsync(cancellationToken))
            .GroupBy(e => e.ExerciseId)
            .ToDictionary(g => g.Key, g => g.Max(e => e.RepsCompleted));

        var result = new List<Recommendation>();
        foreach (var tree in nodes.GroupBy(n => n.SkillTreeId))
        {
            var treeNodes = tree.Select(n => (n.Id, n.ExerciseId, n.OrderIndex, n.MinReps)).ToList();
            var progress = SkillTreePolicy.EvaluateProgress(treeNodes, bestReps);
            var next = SkillTreePolicy.NextNode(progress);
            if (next is null)
            {
                continue;
            }

            var nextNodeData = tree.First(n => n.Id == next.Value.NodeId);
            result.Add(RecommendationPolicy.ExerciseSubstitution(nextNodeData.ExerciseId, nextNodeData.ExerciseName, nextNodeData.UnlockExplanation));
        }

        return result;
    }
}
