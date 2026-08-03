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

        // Computed in memory (entity already materialized) rather than in the query itself,
        // since summing a navigation collection inside a property getter isn't guaranteed to
        // translate to SQL the same way an explicit Sum() in a LINQ query does.
        var amountPaid = invoice.Payments.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount);

        return new InvoiceDetailDto(
            invoice.Id, invoice.InvoiceNumber, invoice.MemberId,
            $"{invoice.Member?.FirstName} {invoice.Member?.LastName}".Trim(),
            invoice.IssueDate, invoice.DueDate, invoice.Status, invoice.Subtotal, invoice.TaxAmount,
            invoice.DiscountAmount, invoice.TotalAmount, invoice.Currency, invoice.Notes,
            invoice.Lines.Select(l => new InvoiceLineDto(l.Id, l.ItemType, l.Description, l.Quantity, l.UnitPrice, l.Quantity * l.UnitPrice)).ToList(),
            invoice.Payments.Select(p => new PaymentDto(p.Id, p.Method, p.Amount, p.PaidAt, p.GatewayTransactionId, p.Status)).ToList(),
            amountPaid, invoice.TotalAmount - amountPaid);
    }
}
