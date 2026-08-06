namespace GymOS.Domain.Experience;

/// <summary>
/// The pure recommendation engine: typed, always-explained nudges built from signals the rest of the
/// Member Experience Engine already computes — RecoveryPolicy (recovery advice), logged training
/// volume (week-over-week trend), and SkillTreePolicy (exercise substitution). Deliberately does not
/// recompute any of those; it only decides what to say and why, given what they already found.
/// "Never reward unsafe lifting alone" carries over here as "never recommend blind": every method
/// either returns nothing or a Recommendation with a concrete Explanation.
///
/// Two members of this family went in the Step 9 review — a per-exercise overload alert and a
/// weakest-muscle-group focus. Not because either was wrong, but because each restated something the
/// member was already reading on the same screen, and both facts are put better by
/// TrainingInsightPolicy, which ranks them against everything else instead of listing them fourth.
/// What is left here is what only this engine knows.
/// </summary>
public static class RecommendationPolicy
{
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

    /// <summary>A trainer has an active plan on file — the self-directed "what to train" suggestion
    /// (ExerciseSubstitution) defers to it rather than competing with it. Recovery and volume signals
    /// are about the member's own body state and stay independent of any plan.</summary>
    public static Recommendation TrainerPlanActive(string planName)
        => new(RecommendationType.TrainerPlanActive, "Follow your trainer's plan",
            $"Your trainer has assigned \"{planName}\" — follow that plan rather than self-directed suggestions.");
}
