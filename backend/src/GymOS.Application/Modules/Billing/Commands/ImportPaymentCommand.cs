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
        await db.LockInvoiceForUpdateAsync(request.InvoiceId, cancellationToken);

        var invoice = await db.Invoices
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken)
            ?? throw new NotFoundException(nameof(Invoice), request.InvoiceId);

        var amount = Math.Round(request.Amount, 2, MidpointRounding.ToEven);

        var completedPayments = invoice.Payments.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount);

        var completedRefunds = await db.Refunds
            .Where(r => r.Payment != null && r.Payment.InvoiceId == invoice.Id && r.Status == RefundStatus.Completed)
            .SumAsync(r => r.Amount, cancellationToken);

        /*
         * The same ceiling the front desk is held to.
         *
         * This handler skips the gateway charge on purpose — the money already moved in the old
         * system — and that exemption had quietly widened into skipping the overpayment rule as
         * well. A CSV row of $5,000 against a $100 invoice landed with no complaint and left it
         * showing Paid with -$4,900 outstanding; no concurrency needed, just a spreadsheet. An import
         * is a lower-trust input than a receptionist, not a higher one.
         */
        var outstanding = InvoiceStatusPolicy.Outstanding(invoice.TotalAmount, completedPayments, completedRefunds);

        if (amount > outstanding)
        {
            throw new ValidationException(
                $"Imported payment of {amount:0.00} exceeds the {outstanding:0.00} still owed on invoice {invoice.InvoiceNumber}.");
        }

        var payment = new Payment
        {
            InvoiceId = invoice.Id,
            Method = request.Method,
            Amount = amount,
            PaidAt = request.PaidAt,
            ReceivedByUserId = null,
            Status = PaymentStatus.Completed
        };

        db.Payments.Add(payment);

        /*
         * InvoiceStatusPolicy, not a fourth hand-rolled rule. The line here was
         * `totalPaid >= TotalAmount ? Paid : PartiallyPaid`, which is blind to refunds and cannot
         * produce Overdue — so an imported part-payment on a long-overdue invoice moved it to
         * PartiallyPaid and out of the collections queue, exactly the disappearing act the live
         * payment path was fixed for.
         */
        invoice.Status = InvoiceStatusPolicy.Derive(
            invoice.TotalAmount, completedPayments + amount, completedRefunds,
            invoice.DueDate, DateOnly.FromDateTime(request.PaidAt.UtcDateTime));

        await db.SaveChangesAsync(cancellationToken);

        return payment.Id;
    }
}
