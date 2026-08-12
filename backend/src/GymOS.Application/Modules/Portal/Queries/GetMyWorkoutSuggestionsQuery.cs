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
                // The Weighted-only filter below means these are always present; coalescing is
                // belt-and-braces for a row written before that guard existed.
                TotalReps = g.Sum(x => x.SetsCompleted * (x.RepsCompleted ?? 0))
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

            /*
             * Only movements measured in kilograms.
             *
             * ProgressiveOverloadPolicy compares MaxWeightKg and TotalReps and has no other terms, so
             * running it over a treadmill produced the defect this filter exists for: two runs logged
             * alike read as a plateau, and the member was told to add 2.5% to a weight that was never
             * meaningful. A plank and a run are not stalled because their kilograms did not change.
             *
             * Filtering here rather than inside the policy on purpose — the policy is a pure rule
             * about a pair of numbers and should stay that way. Deciding WHICH movements those
             * numbers describe is this query's job, and it is the only place that has the exercise.
             */
            if (exercise.LoadType != ExerciseLoadType.Weighted)
            {
                continue;
            }

            var sessionsOldestFirst = group.OrderBy(s => s.LoggedAt).ToList();
            var performances = sessionsOldestFirst.Select(s => new ExercisePerformance(s.MaxWeightKg, s.TotalReps)).ToList();
            var suggestion = ProgressiveOverloadPolicy.Evaluate(performances);

            var last = sessionsOldestFirst[^1];
            decimal? suggestedNextWeight = suggestion == OverloadSuggestion.ReadyToIncreaseWeight && last.MaxWeightKg > 0
                ? ProgressiveOverloadPolicy.SuggestedNextWeightKg(last.MaxWeightKg)
                : null;

            // Resolved, not passed through. The Train screen pulls a suggestion out of today's list
            // when its muscle group is fatigued, and it matches on this value against the recovery
            // breakdown — which resolves through the same vocabulary. Two different readings of the
            // same free-text column would quietly stop the two agreeing.
            var muscle = MuscleGroupVocabulary.Resolve(exercise.MuscleGroup);

            results.Add(new MyExerciseSuggestionDto(
                exercise.Id, exercise.Name, muscle.DisplayName, muscle.Key, suggestion,
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
