namespace GymOS.Domain.Experience;

/// <summary>
/// Estimated one-rep max via the Epley formula (weight·(1 + reps/30)), rounded to 2dp. A single pure
/// definition shared by the mastery projection and personal-record detection so the "estimated 1RM"
/// they report can never disagree. Zero/negative reps (or a bodyweight set with no load) fall back to
/// the weight itself.
/// </summary>
public static class OneRepMax
{
    public static decimal Epley(decimal weightKg, int reps)
        => reps <= 0 ? weightKg : Math.Round(weightKg * (1 + reps / 30m), 2);
}
