using GymOS.Domain.Billing;

namespace GymOS.Application.Modules.Billing.Dtos;

public record InvoiceLineDto(
    Guid Id, InvoiceLineItemType ItemType, string Description, int Quantity, decimal UnitPrice, decimal LineTotal, Guid? InventoryItemId);

public record PaymentDto(Guid Id, PaymentMethod Method, decimal Amount, DateTimeOffset PaidAt, string? GatewayTransactionId, PaymentStatus Status);

public record RefundDto(Guid Id, Guid PaymentId, decimal Amount, string Reason, DateTimeOffset RefundedAt, RefundStatus Status);

public record InvoiceListItemDto(
    Guid Id, string InvoiceNumber, Guid MemberId, string MemberName, DateOnly IssueDate, DateOnly DueDate,
    InvoiceStatus Status, decimal TotalAmount, decimal AmountPaid, decimal AmountOutstanding, string Currency);

public record InvoiceDetailDto(
    Guid Id, string InvoiceNumber, Guid MemberId, string MemberName, DateOnly IssueDate, DateOnly DueDate,
    InvoiceStatus Status, decimal Subtotal, decimal TaxAmount, decimal DiscountAmount, decimal TotalAmount,
    string Currency, string? Notes,
    IReadOnlyList<InvoiceLineDto> Lines, IReadOnlyList<PaymentDto> Payments, IReadOnlyList<RefundDto> Refunds,
    decimal AmountPaid, decimal AmountOutstanding);

/// <summary>
/// One line of the chase list: a renewal whose card failed, and everything a person needs to act on
/// it without opening the database.
///
/// <see cref="MaxAttempts"/> travels with the row rather than being hardcoded on the client, so
/// "2 of 4" stays a real fraction if <see cref="Domain.Billing.BillingRetryPolicy"/> ever changes
/// its retry budget — the alternative is a screen that quietly misreports how much rope is left.
/// </summary>
public record DunningAttemptDto(
    Guid Id, Guid MemberId, string MemberName, Guid InvoiceId, string InvoiceNumber,
    decimal Amount, string Currency, int FailedAttempts, int MaxAttempts, string? LastFailureReason,
    DateOnly NextAttemptDate, DateOnly? LastAttemptDate, RecurringBillingStatus Status,
    bool MembershipSuspended);
