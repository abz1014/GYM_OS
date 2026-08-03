using GymOS.Domain.Common;

namespace GymOS.Domain.Billing;

public class InvoiceLine : BaseEntity
{
    public Guid InvoiceId { get; set; }

    public Invoice? Invoice { get; set; }

    public InvoiceLineItemType ItemType { get; set; }

    public Guid? InventoryItemId { get; set; }

    public string Description { get; set; } = string.Empty;

    public int Quantity { get; set; } = 1;

    public decimal UnitPrice { get; set; }

    public decimal LineTotal => Quantity * UnitPrice;
}
