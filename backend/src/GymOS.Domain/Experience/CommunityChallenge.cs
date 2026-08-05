using GymOS.Domain.Common;

namespace GymOS.Domain.Experience;

/// <summary>
/// A staff-created, opt-in community challenge (blueprint Phase 12): "log N workouts between these
/// dates." Tenant-scoped and optionally branch-scoped — a null BranchId is tenant-wide (visible to
/// every branch), a set one restricts it to a single branch. Deliberately one completion metric
/// (workout count in a date window) rather than a generic goal system: it reuses the existing
/// WorkoutLoggedEvent as its only trigger, so completion needs no new domain event.
/// </summary>
public class CommunityChallenge : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid? BranchId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    /// <summary>How many workouts a participant must log within [StartDate, EndDate] to complete it.</summary>
    public int TargetWorkoutCount { get; set; }

    public Guid? CreatedByUserId { get; set; }
}
