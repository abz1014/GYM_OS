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

    /// <summary>The NextDueDate value that was last notified on. Compared against NextDueDate
    /// (rather than checking notification history) so a fresh due-cycle always re-notifies once,
    /// even though the schedule's Id — and therefore any notification keyed only on it — never changes.</summary>
    public DateOnly? LastNotifiedDueDate { get; set; }
}
