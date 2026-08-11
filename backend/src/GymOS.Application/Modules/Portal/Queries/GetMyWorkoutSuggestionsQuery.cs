using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Portal.Dtos;
using GymOS.Domain.Workouts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Portal.Queries;

/// <summary>
/// Per-exercise progressive-overload suggestions from the member's own logged history — the
/// "should I add weight next time" answer a member would otherwise have to work out themselves
/// from a raw log list. Selection logic is entirely ProgressiveOverloadPolicy's; this query only
/// assembles each exercise's session history (oldest first) and feeds it in.
/// </summary>
public record GetMyWorkoutSuggestionsQuery : IQuery<List<MyExerciseSuggestionDto>>;

public class GetMyWorkoutSuggestionsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetMyWorkoutSuggestionsQuery, List<MyExerciseSuggestionDto>>
{
    public async Task<List<MyExerciseSuggestionDto>> Handle(GetMyWorkoutSuggestionsQuery request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);

        var logs = await db.WorkoutLogs.AsNoTracking()
            .Where(l => l.MemberId == memberId)
            .Include(l => l.Entries)
            .ToListAsync(cancellationToken);

        // One row per (session, exercise): a log may contain more than one entry for the same
        // exercise (e.g. a warm-up set logged separately from the working set) — the heaviest weight
        // that session is the one worth comparing, with total reps across those entries as the
        // volume signal.
        var sessionsByExercise = logs
            .SelectMany(l => l.Entries.Select(e => new { l.LoggedAt, e.ExerciseId, e.WeightKg, e.SetsCompleted, e.RepsCompleted }))
            .GroupBy(x => new { x.LoggedAt, x.ExerciseId })
            .Select(g => new
            {
                g.Key.ExerciseId,
                g.Key.LoggedAt,
                MaxWeightKg = g.Max(x => x.WeightKg) ?? 0m,
                TotalReps = g.Sum(x => x.SetsCompleted * x.RepsCompleted)
            })
            .GroupBy(x => x.ExerciseId)
            .ToList();

        if (sessionsByExercise.Count == 0)
        {
            return [];
        }

        var exerciseIds = sessionsByExercise.Select(g => g.Key).ToList();
        var exercises = await db.Exercises.AsNoTracking()
            .Where(e => exerciseIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, cancellationToken);

        var results = new List<MyExerciseSuggestionDto>();
        foreach (var group in sessionsByExercise)
        {
            if (!exercises.TryGetValue(group.Key, out var exercise))
            {
                continue; // exercise deleted since it was logged
            }

            var sessionsOldestFirst = group.OrderBy(s => s.LoggedAt).ToList();
            var performances = sessionsOldestFirst.Select(s => new ExercisePerformance(s.MaxWeightKg, s.TotalReps)).ToList();
            var suggestion = ProgressiveOverloadPolicy.Evaluate(performances);

            var last = sessionsOldestFirst[^1];
            decimal? suggestedNextWeight = suggestion == OverloadSuggestion.ReadyToIncreaseWeight && last.MaxWeightKg > 0
                ? ProgressiveOverloadPolicy.SuggestedNextWeightKg(last.MaxWeightKg)
                : null;

            results.Add(new MyExerciseSuggestionDto(
                exercise.Id, exercise.Name, exercise.MuscleGroup, suggestion,
                last.MaxWeightKg > 0 ? last.MaxWeightKg : null, last.TotalReps, suggestedNextWeight, last.LoggedAt));
        }

        // Most useful first, then most recent within a verdict. Recency alone put whatever the member
        // trained last at the top, which is fine for a list of six and wrong for a screen that shows
        // one — the headline would be an arbitrary exercise rather than the one worth acting on.
        return results
            .OrderBy(r => ProgressiveOverloadPolicy.LeadPriority(r.Suggestion))
            .ThenByDescending(r => r.LastLoggedAt)
            .ToList();
    }
}
