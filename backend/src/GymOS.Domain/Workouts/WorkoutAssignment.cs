using GymOS.Domain.Common;

namespace GymOS.Domain.Workouts;

/// <summary>
/// A trainer-assigned workout plan: "this template is on this member's plan starting this date."
/// Deliberately references WorkoutTemplate rather than duplicating its exercise list — the template
/// is the prescribed sets/reps, the assignment is just who it's for and when. Mirrors DietPlan's
/// shape (MemberId, StartDate/EndDate, CreatedByUserId) for the same reason: a member-specific,
/// staff-assigned plan the member can see in their own portal, distinct from WorkoutLog (a record
/// of what already happened) and WorkoutTemplate (the shared, unassigned exercise-list catalog).
/// </summary>
public class WorkoutAssignment : BaseEntity, ITenantScoped
{
    /// <summary>
    /// Direct tenant scoping, so isolation is a property of the schema rather than of every query
    /// that happens to start from Member.
    ///
    /// This table was reachable only through a tenant-scoped Member, which made it safe in practice
    /// and unguarded in principle: one future query beginning here instead of at Member would cross
    /// tenants silently, with nothing failing. Same class of gap as the cross-branch IDOR, same fix —
    /// enforce it in the model so nobody has to remember.
    /// </summary>
    public Guid TenantId { get; set; }

    public Guid MemberId { get; set; }

    public Guid WorkoutTemplateId { get; set; }

    public WorkoutTemplate? WorkoutTemplate { get; set; }

    public Guid? AssignedByUserId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string? Notes { get; set; }
}
