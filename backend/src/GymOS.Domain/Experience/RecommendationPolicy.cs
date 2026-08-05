using GymOS.Domain.Workouts;

namespace GymOS.Domain.Experience;

/// <summary>One exercise's progressive-overload read for the recommendation engine — the same signal
/// ProgressiveOverloadPolicy already produces per exercise, carried with enough context to phrase a
/// recommendation.</summary>
public readonly record struct ExerciseOverloadSignal(Guid ExerciseId, string ExerciseName, OverloadSuggestion Suggestion, decimal? LastWeightKg);

/// <summary>One muscle group's mastery, as already aggregated by MasteryPolicy/GetMyMasteryQuery.</summary>
public readonly record struct MuscleGroupSignal(string MuscleGroup, int MasteryPercent);

/// <summary>
/// The pure recommendation engine (blueprint Phase 6/7): synthesizes typed, always-explained nudges
/// from signals the rest of the Member Experience Engine already computes — ProgressiveOverloadPolicy
/// (plateaus), MasteryPolicy (weakest muscle group), RecoveryPolicy (recovery advice), logged training
/// volume (week-over-week trend), and SkillTreePolicy (exercise substitution). Deliberately does not
/// recompute any of those — it only decides what to say and why, given what they already found.
/// "Never reward unsafe lifting alone" carries over here as "never recommend blind": every method
/// either returns nothing or a Recommendation with a concrete Explanation.
/// </summary>
public static class RecommendationPolicy
{
    /// <summary>A plateau alert per exercise that's held identical weight/reps for two sessions running
    /// — the same signal the per-exercise "add weight next time" card already shows, just promoted to
    /// a top-level nudge. Exercises still progressing or lacking history are silently excluded.</summary>
    public static IReadOnlyList<Recommendation> PlateauAlerts(IReadOnlyList<ExerciseOverloadSignal> signals)
        => signals
            .Where(s => s.Suggestion == OverloadSuggestion.ReadyToIncreaseWeight)
            .Select(s => new Recommendation(
                RecommendationType.PlateauAlert,
                $"{s.ExerciseName}: ready to add weight",
                $"You've held {(s.LastWeightKg is { } w ? $"{w}kg" : "the same weight")} for two sessions running on {s.ExerciseName} — try a small increase next time.",
                s.ExerciseId))
            .ToList();

    /// <summary>The member's weakest trained muscle group, as a nudge toward balance. Null when the
    /// member has no mastery data yet (nothing to compare) — "trained" is implicit, since a muscle
    /// group only appears in the mastery breakdown once it has logged sessions.</summary>
    public static Recommendation? WeeklyFocus(IReadOnlyList<MuscleGroupSignal> muscleGroups)
    {
        if (muscleGroups.Count == 0)
        {
            return null;
        }

        var weakest = muscleGroups.OrderBy(g => g.MasteryPercent).ThenBy(g => g.MuscleGroup).First();
        return new Recommendation(
            RecommendationType.WeeklyFocus,
            $"Focus on {weakest.MuscleGroup} this week",
            $"{weakest.MuscleGroup} is your weakest trained muscle group at {weakest.MasteryPercent}% mastery — give it some attention to stay balanced.");
    }

    /// <summary>Week-over-week training volume trend. Null when there's no prior week to compare
    /// against, or the swing is unremarkable (0.7x–1.5x) — only a meaningful jump or drop is worth
    /// surfacing.</summary>
    public static Recommendation? VolumeTrend(decimal currentWeekVolume, decimal previousWeekVolume)
    {
        if (previousWeekVolume <= 0)
        {
            return null;
        }

        var ratio = currentWeekVolume / previousWeekVolume;

        if (ratio < 0.7m)
        {
            return new Recommendation(
                RecommendationType.VolumeSuggestion,
                "Your training volume dropped",
                $"This week's volume ({currentWeekVolume:N0}kg) is well below last week's ({previousWeekVolume:N0}kg) — ease back toward your usual load when you're ready.");
        }

        if (ratio > 1.5m)
        {
            return new Recommendation(
                RecommendationType.VolumeSuggestion,
                "Your training volume jumped",
                $"This week's volume ({currentWeekVolume:N0}kg) is well above last week's ({previousWeekVolume:N0}kg) — great push, just watch your recovery so it sticks.");
        }

        return null;
    }

    /// <summary>Recovery advice, surfaced only when the member's overall recovery status calls for
    /// action (Fatigued/OvertrainingRisk) — Fresh/Ready need no nudge, so this returns null for them.</summary>
    public static Recommendation? RecoveryAdvice(RecoveryStatus status, string reason)
        => status is RecoveryStatus.Fatigued or RecoveryStatus.OvertrainingRisk
            ? new Recommendation(RecommendationType.RecoveryAdvice, "Consider a recovery day", reason)
            : null;

    /// <summary>The next exercise in a skill tree the member is ready to progress to, given the node
    /// they've already unlocked furthest into. <paramref name="unlockExplanation"/> is the node's own
    /// authored explanation (why this exercise, what it builds toward).</summary>
    public static Recommendation ExerciseSubstitution(Guid exerciseId, string exerciseName, string unlockExplanation)
        => new(RecommendationType.ExerciseSubstitution, $"Try {exerciseName} next", unlockExplanation, exerciseId);

    /// <summary>A trainer has an active plan on file — self-directed "what to train" recommendations
    /// (WeeklyFocus, ExerciseSubstitution) defer to it rather than compete with it. Recovery/plateau/
    /// volume signals are about the member's own body state and stay independent of any plan.</summary>
    public static Recommendation TrainerPlanActive(string planName)
        => new(RecommendationType.TrainerPlanActive, "Follow your trainer's plan",
            $"Your trainer has assigned \"{planName}\" — follow that plan rather than self-directed suggestions.");
}
