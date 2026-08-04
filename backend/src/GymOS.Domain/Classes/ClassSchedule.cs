using GymOS.Domain.Common;
using GymOS.Domain.Trainers;

namespace GymOS.Domain.Classes;

/// <summary>
/// A recurring weekly class slot: "Spin, every Monday 18:00, Studio A, 20 spots, Coach Alex."
/// This is the RULE, not a bookable event — a background job materialises concrete, dated
/// ClassSession rows from it a rolling window ahead (see ClassSessionPlanner). Modelled the same
/// way MaintenanceSchedule spawns work-cycles, and the same way TrainerSchedule stores a
/// DayOfWeek + TimeOnly, so the two calendar concepts in the system stay consistent.
/// </summary>
public class ClassSchedule : BaseEntity, IBranchScoped
{
    public Guid TenantId { get; set; }

    public Guid BranchId { get; set; }

    public Guid ClassTypeId { get; set; }

    public ClassType? ClassType { get; set; }

    /// <summary>Optional — a class may run without a named instructor assigned yet.</summary>
    public Guid? TrainerId { get; set; }

    public Trainer? Trainer { get; set; }

    public DayOfWeek DayOfWeek { get; set; }

    public TimeOnly StartTime { get; set; }

    public int DurationMinutes { get; set; }

    public int Capacity { get; set; }

    public string? Location { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>How far ahead concrete sessions have already been generated for this schedule.
    /// The generation job resumes from here rather than re-walking the whole window each run.</summary>
    public DateOnly? GeneratedThroughDate { get; set; }
}
