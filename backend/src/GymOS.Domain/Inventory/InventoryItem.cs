using GymOS.Domain.Common;

namespace GymOS.Domain.Inventory;

public class InventoryItem : BaseEntity, IBranchScoped, IAuditable
{
    public Guid TenantId { get; set; }

    public Guid BranchId { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public InventoryCategory Category { get; set; }

    public int QuantityOnHand { get; set; }

    public int ReorderLevel { get; set; }

    public decimal? UnitCost { get; set; }

    public decimal? UnitPrice { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    public bool IsLowStock => QuantityOnHand <= ReorderLevel;
}
