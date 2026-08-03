using GymOS.Domain.Common;
using GymOS.Domain.Members;

namespace GymOS.Domain.Billing;

public class Invoice : BaseEntity, IBranchScoped, IAuditable
{
    public Guid TenantId { get; set; }

    public Guid BranchId { get; set; }

    public Guid MemberId { get; set; }

    public Member? Member { get; set; }

    public string InvoiceNumber { get; set; } = string.Empty;

    public DateOnly IssueDate { get; set; }

    public DateOnly DueDate { get; set; }

    public InvoiceStatus Status { get; set; }

    public decimal Subtotal { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    public ICollection<InvoiceLine> Lines { get; set; } = [];

    public ICollection<Payment> Payments { get; set; } = [];

    public ICollection<PaymentReminder> Reminders { get; set; } = [];

    public decimal AmountPaid => Payments.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount);

    public decimal AmountOutstanding => TotalAmount - AmountPaid;
}
