namespace GymOS.Domain.Experience;

/// <summary>A single exercise's aggregated stats within one workout session.</summary>
public record ExerciseSessionStats(decimal MaxWeightKg, decimal BestEstimatedOneRepMax, decimal SessionVolume);

/// <summary>
/// Pure rules for personal-record detection: reduce a session's sets to per-metric values, and decide
/// whether a value beats the prior best. A record is only "beaten" when strictly greater than the
/// prior best — which is also what makes detection idempotent: once a PR row is written, re-running
/// the same session sees its own value as the prior best and matches (not exceeds) it, so nothing is
/// duplicated.
/// </summary>
public static class PersonalRecordPolicy
{
    /// <summary>
    /// Reduces a session's entries to the three figures a record can be set on.
    ///
    /// Reps are nullable because a run and a plank have none. An entry without them still reports its
    /// LOAD — a farmer's carry is genuinely loaded — but contributes no estimated 1RM and no volume,
    /// because both are defined as functions of reps and there is no honest value to substitute. Zero
    /// would be a measurement of nothing rather than the absence of one, and it would enter a sum.
    /// </summary>
    public static ExerciseSessionStats StatsFor(IEnumerable<(int Sets, int? Reps, decimal? WeightKg)> entries)
    {
        decimal maxWeight = 0, bestOneRm = 0, volume = 0;

        foreach (var (sets, reps, weightKg) in entries)
        {
            var weight = weightKg ?? 0;
            if (weight > maxWeight)
            {
                maxWeight = weight;
            }

            if (reps is null)
            {
                continue;
            }

            var oneRm = OneRepMax.Epley(weight, reps.Value);
            if (oneRm > bestOneRm)
            {
                bestOneRm = oneRm;
            }

            volume += sets * reps.Value * weight;
        }

        return new ExerciseSessionStats(maxWeight, bestOneRm, volume);
    }

    public static decimal ValueFor(ExerciseSessionStats stats, PersonalRecordType type) => type switch
    {
        PersonalRecordType.MaxWeight => stats.MaxWeightKg,
        PersonalRecordType.EstimatedOneRepMax => stats.BestEstimatedOneRepMax,
        PersonalRecordType.SessionVolume => stats.SessionVolume,
        _ => 0
    };

    /// <summary>A metric is a new record when it strictly exceeds the prior best and is a real (>0)
    /// number — so an unweighted/zero session never registers a "record".</summary>
    public static bool Beats(decimal candidate, decimal priorBest) => candidate > priorBest && candidate > 0;
}
