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
