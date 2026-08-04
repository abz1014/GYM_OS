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
public class WorkoutAssignment : BaseEntity
{
    public Guid MemberId { get; set; }

    public Guid WorkoutTemplateId { get; set; }

    public WorkoutTemplate? WorkoutTemplate { get; set; }

    public Guid? AssignedByUserId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string? Notes { get; set; }
}
