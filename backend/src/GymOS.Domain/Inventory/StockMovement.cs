using GymOS.Domain.Common;

namespace GymOS.Domain.Inventory;

public class StockMovement : BaseEntity
{
    public Guid InventoryItemId { get; set; }

    public InventoryItem? InventoryItem { get; set; }

    public StockMovementType Type { get; set; }

    public int Quantity { get; set; }

    public string? Reason { get; set; }

    public DateTimeOffset MovedAt { get; set; }

    public Guid? RecordedByUserId { get; set; }
}
