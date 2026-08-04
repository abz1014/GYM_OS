using GymOS.Domain.Common;

namespace GymOS.Domain.Experience;

/// <summary>
/// A member's mastery projection for one exercise — the atomic unit the blueprint's ExerciseMastery/
/// MuscleGroupMastery/MachineMastery all derive from (muscle-group and machine breakdowns are
/// aggregated from these on read via Exercise.MuscleGroup / Exercise.Equipment, rather than
/// duplicated into their own tables). Maintained by recomputing from the member's full logged history
/// for the exercise whenever they train it, so it's idempotent and rebuildable — never blind
/// increments that could double-count on a replay. Mastery % itself is computed on read (it depends on
/// recency), so only the raw accumulators live here.
/// </summary>
public class ExerciseMastery : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid MemberId { get; set; }

    public Guid ExerciseId { get; set; }

    public int Sessions { get; set; }

    public int TotalSets { get; set; }

    public long TotalReps { get; set; }

    public decimal TotalVolume { get; set; }

    public decimal BestWeightKg { get; set; }

    public decimal BestEstimatedOneRepMax { get; set; }

    public DateTimeOffset LastTrainedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
