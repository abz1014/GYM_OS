using GymOS.Domain.Common;
using GymOS.Domain.Equipment;

namespace GymOS.Domain.Inventory;

public class PurchaseRecord : BaseEntity
{
    public Guid InventoryItemId { get; set; }

    public InventoryItem? InventoryItem { get; set; }

    public Guid? SupplierId { get; set; }

    public Supplier? Supplier { get; set; }

    public int Quantity { get; set; }

    public decimal UnitCost { get; set; }

    public DateOnly PurchasedAt { get; set; }

    public string? InvoiceReference { get; set; }
}
