using GymOS.Domain.Common;
using GymOS.Domain.Equipment;

namespace GymOS.Domain.Maintenance;

public class MaintenanceSchedule : BaseEntity
{
    public Guid AssetId { get; set; }

    public Asset? Asset { get; set; }

    public string RecurrenceRule { get; set; } = string.Empty;

    public DateOnly NextDueDate { get; set; }

    public bool IsActive { get; set; } = true;
}
