using GymOS.Domain.Common;

namespace GymOS.Domain.Billing;

public class Refund : BaseEntity
{
    public Guid PaymentId { get; set; }

    public Payment? Payment { get; set; }

    public decimal Amount { get; set; }

    public string Reason { get; set; } = string.Empty;

    public Guid? ApprovedByUserId { get; set; }

    public DateTimeOffset RefundedAt { get; set; }

    public RefundStatus Status { get; set; } = RefundStatus.Completed;
}
