namespace GymOS.Domain.Workouts;

/// <summary>A single session's logged performance on one exercise (aggregated if logged in more than one set entry that session).</summary>
public record ExercisePerformance(decimal MaxWeightKg, int TotalReps);

public enum OverloadSuggestion
{
    /// <summary>Fewer than 2 logged sessions for this exercise — nothing to compare against yet.</summary>
    InsufficientData,

    /// <summary>Weight (or reps at the same weight) increased since last session — already progressing.</summary>
    Progressing,

    /// <summary>Same weight and rep count held across the last two sessions — a plateau; add weight next time.</summary>
    ReadyToIncreaseWeight,

    /// <summary>Weight or reps dropped since last session — back off rather than push through.</summary>
    ConsiderDeload
}

/// <summary>
/// A simple double-progression heuristic: keep the reps/weight of the last two logged sessions for
/// one exercise and decide what the member should do next time. Pure (facts in, suggestion out) so
/// the rule is unit-tested independent of how workout history gets queried. Deliberately looks at
/// only the most recent pair of sessions rather than a longer trend line — a member's next session
/// is the one thing this can actually influence, and a longer lookback adds noise (illness, a
/// deliberate light day) without changing the actionable answer.
/// </summary>
public static class ProgressiveOverloadPolicy
{
    /// <summary>Suggested weight bump when a plateau is detected, as a fraction of the current working weight.</summary>
    public const decimal SuggestedIncreaseFraction = 0.025m;

    /// <param name="sessionsOldestFirst">This exercise's logged sessions, oldest first. Only the last two are compared.</param>
    public static OverloadSuggestion Evaluate(IReadOnlyList<ExercisePerformance> sessionsOldestFirst)
    {
        if (sessionsOldestFirst.Count < 2)
        {
            return OverloadSuggestion.InsufficientData;
        }

        var previous = sessionsOldestFirst[^2];
        var latest = sessionsOldestFirst[^1];

        if (latest.MaxWeightKg > previous.MaxWeightKg
            || (latest.MaxWeightKg == previous.MaxWeightKg && latest.TotalReps > previous.TotalReps))
        {
            return OverloadSuggestion.Progressing;
        }

        if (latest.MaxWeightKg < previous.MaxWeightKg || latest.TotalReps < previous.TotalReps)
        {
            return OverloadSuggestion.ConsiderDeload;
        }

        // Identical weight and reps two sessions running: the plateau double progression watches for.
        return OverloadSuggestion.ReadyToIncreaseWeight;
    }

    /// <summary>Rounded to the nearest 0.5 kg — matches how gym plates/dumbbells actually increment.</summary>
    public static decimal SuggestedNextWeightKg(decimal currentWeightKg)
        => Math.Round(currentWeightKg * (1 + SuggestedIncreaseFraction) * 2, MidpointRounding.AwayFromZero) / 2;
}
