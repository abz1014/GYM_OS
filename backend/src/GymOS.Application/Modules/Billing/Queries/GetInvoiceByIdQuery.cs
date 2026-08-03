using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Billing.Dtos;
using GymOS.Domain.Billing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Billing.Queries;

public record GetInvoiceByIdQuery(Guid Id) : IQuery<InvoiceDetailDto>;

public class GetInvoiceByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetInvoiceByIdQuery, InvoiceDetailDto>
{
    public async Task<InvoiceDetailDto> Handle(GetInvoiceByIdQuery request, CancellationToken cancellationToken)
    {
        var invoice = await db.Invoices.AsNoTracking()
            .Include(i => i.Member)
            .Include(i => i.Lines)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Invoice), request.Id);

        var paymentIds = invoice.Payments.Select(p => p.Id).ToList();
        var refunds = await db.Refunds.AsNoTracking()
            .Where(r => paymentIds.Contains(r.PaymentId))
            .OrderByDescending(r => r.RefundedAt)
            .ToListAsync(cancellationToken);

        // Computed in memory (entity already materialized) rather than in the query itself,
        // since summing a navigation collection inside a property getter isn't guaranteed to
        // translate to SQL the same way an explicit Sum() in a LINQ query does.
        var amountPaid = invoice.Payments.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount)
            - refunds.Where(r => r.Status == RefundStatus.Completed).Sum(r => r.Amount);

        return new InvoiceDetailDto(
            invoice.Id, invoice.InvoiceNumber, invoice.MemberId,
            $"{invoice.Member?.FirstName} {invoice.Member?.LastName}".Trim(),
            invoice.IssueDate, invoice.DueDate, invoice.Status, invoice.Subtotal, invoice.TaxAmount,
            invoice.DiscountAmount, invoice.TotalAmount, invoice.Currency, invoice.Notes,
            invoice.Lines.Select(l => new InvoiceLineDto(l.Id, l.ItemType, l.Description, l.Quantity, l.UnitPrice, l.Quantity * l.UnitPrice, l.InventoryItemId)).ToList(),
            invoice.Payments.Select(p => new PaymentDto(p.Id, p.Method, p.Amount, p.PaidAt, p.GatewayTransactionId, p.Status)).ToList(),
            refunds.Select(r => new RefundDto(r.Id, r.PaymentId, r.Amount, r.Reason, r.RefundedAt, r.Status)).ToList(),
            amountPaid, invoice.TotalAmount - amountPaid);
    }
}
