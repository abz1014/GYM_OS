using GymOS.Domain.Common;

namespace GymOS.Domain.Billing;

public class Payment : BaseEntity
{
    public Guid InvoiceId { get; set; }

    public Invoice? Invoice { get; set; }

    public PaymentMethod Method { get; set; }

    public decimal Amount { get; set; }

    public DateTimeOffset PaidAt { get; set; }

    public Guid? ReceivedByUserId { get; set; }

    // Null for pure manual cash entries; populated by the payment gateway abstraction once a real
    // gateway plugs in behind the current no-op demo simulation, without needing a schema change.
    public string? GatewayTransactionId { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Completed;

    /// <summary>
    /// Refunds issued against this payment. The inverse of the FK Refund already carried — adding it
    /// changes no schema, and it is what lets the invoice LIST subtract refunds in a single
    /// translated query instead of disagreeing with the detail view about how much is owed.
    /// </summary>
    public ICollection<Refund> Refunds { get; set; } = [];
}
