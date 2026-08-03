using GymOS.Domain.Common;
using GymOS.Domain.Equipment;

namespace GymOS.Domain.Maintenance;

public class WorkOrder : BaseEntity, IBranchScoped, IAuditable
{
    public Guid TenantId { get; set; }

    public Guid BranchId { get; set; }

    public Guid AssetId { get; set; }

    public Asset? Asset { get; set; }

    /// <summary>The recurring inspection this work order was raised in response to, if any — completing it (verified) advances that schedule's next due date.</summary>
    public Guid? MaintenanceScheduleId { get; set; }

    public MaintenanceSchedule? MaintenanceSchedule { get; set; }

    public WorkOrderType Type { get; set; }

    public WorkOrderPriority Priority { get; set; } = WorkOrderPriority.Medium;

    public WorkOrderStatus Status { get; set; } = WorkOrderStatus.Open;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Guid? AssignedToUserId { get; set; }

    public DateOnly? ScheduledDate { get; set; }

    public DateOnly? CompletedDate { get; set; }

    public decimal? Cost { get; set; }

    public string? VerificationNotes { get; set; }

    public Guid? VerifiedByUserId { get; set; }

    public DateTimeOffset? VerifiedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    public ICollection<DowntimeLog> DowntimeLogs { get; set; } = [];
}
