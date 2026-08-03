using GymOS.Domain.Common;

namespace GymOS.Domain.Maintenance;

public class DowntimeLog : BaseEntity
{
    public Guid AssetId { get; set; }

    public Guid? WorkOrderId { get; set; }

    public WorkOrder? WorkOrder { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    public string? Reason { get; set; }

    public TimeSpan? Duration => EndedAt.HasValue ? EndedAt.Value - StartedAt : null;
}
