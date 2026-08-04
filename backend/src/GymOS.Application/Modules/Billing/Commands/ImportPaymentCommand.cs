using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Billing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Billing.Commands;

/// <summary>
/// Records a historical payment from a migrated legacy system at its original paid date —
/// deliberately side-effect-free (no payment gateway charge, no live dashboard notification, no
/// "received by" staff attribution) unlike RecordPaymentCommand, which is the real-time front-desk
/// payment flow. Charging a card for a payment that already happened in the old system would be a
/// serious bug, not a migration nicety.
/// </summary>
public record ImportPaymentCommand(Guid InvoiceId, PaymentMethod Method, decimal Amount, DateTimeOffset PaidAt) : ICommand<Guid>;

public class ImportPaymentCommandValidator : AbstractValidator<ImportPaymentCommand>
{
    public ImportPaymentCommandValidator()
    {
        RuleFor(x => x.InvoiceId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}

public class ImportPaymentCommandHandler(IApplicationDbContext db) : IRequestHandler<ImportPaymentCommand, Guid>
{
    public async Task<Guid> Handle(ImportPaymentCommand request, CancellationToken cancellationToken)
    {
        var invoice = await db.Invoices
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken)
            ?? throw new NotFoundException(nameof(Invoice), request.InvoiceId);

        var payment = new Payment
        {
            InvoiceId = invoice.Id,
            Method = request.Method,
            Amount = request.Amount,
            PaidAt = request.PaidAt,
            ReceivedByUserId = null,
            Status = PaymentStatus.Completed
        };

        db.Payments.Add(payment);

        var totalPaid = invoice.Payments.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount) + request.Amount;
        invoice.Status = totalPaid >= invoice.TotalAmount ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;

        await db.SaveChangesAsync(cancellationToken);

        return payment.Id;
    }
}
