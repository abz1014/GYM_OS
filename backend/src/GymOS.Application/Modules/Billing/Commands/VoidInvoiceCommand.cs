using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Billing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Billing.Commands;

/// <summary>
/// Withdraw an invoice that should never have been raised.
///
/// Invoices had exactly one terminal exit — being paid. Anything raised in error (a duplicate, a
/// wrong member, a renewal for someone who had already walked out) stayed in the overdue queue
/// forever, permanently inflating "outstanding" and giving staff a debt to chase that did not
/// exist. The invoices list even offers a "Cancelled" filter tab, and nothing in the system could
/// put a row behind it.
///
/// The refusals below are the point of the command, not decoration around it:
///
///  - An invoice with money against it is NOT voidable. Cancelling it would erase the debt while
///    the payment row survives, so the member's money would exist with nothing to explain it and
///    the takings would stop reconciling against the invoices. That case has a correct verb already
///    — issue a refund, then void what is left, in that order.
///  - An already-Cancelled or Refunded invoice is left alone. Both are terminal, and re-voiding one
///    would append a second reason to Notes and rewrite history for no gain.
/// </summary>
public record VoidInvoiceCommand(Guid InvoiceId, string Reason) : ICommand<Unit>;

public class VoidInvoiceCommandValidator : AbstractValidator<VoidInvoiceCommand>
{
    public VoidInvoiceCommandValidator()
    {
        RuleFor(x => x.InvoiceId).NotEmpty();

        // A reason is mandatory because it is the only record of why the money vanished from the
        // books. "Voided, no note" is indistinguishable from a mistake six months later.
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public class VoidInvoiceCommandHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<VoidInvoiceCommand, Unit>
{
    public async Task<Unit> Handle(VoidInvoiceCommand request, CancellationToken cancellationToken)
    {
        /*
         * Locked before the payment total that decides the outcome is read — the same target and the
         * same reason as RecordPaymentCommand and IssueRefundCommand.
         *
         * Without it, a payment landing at the front desk between the read below and the write could
         * be settled against an invoice this handler is in the middle of cancelling: money taken for
         * a document that no longer owes anything. Sharing one lock target with the other two money
         * handlers means there is no lock ordering to get wrong.
         */
        await db.LockInvoiceForUpdateAsync(request.InvoiceId, cancellationToken);

        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken)
            ?? throw new NotFoundException(nameof(Invoice), request.InvoiceId);

        if (invoice.Status is InvoiceStatus.Cancelled)
        {
            throw new ValidationException($"Invoice {invoice.InvoiceNumber} has already been voided.");
        }

        if (invoice.Status is InvoiceStatus.Refunded)
        {
            throw new ValidationException(
                $"Invoice {invoice.InvoiceNumber} was refunded in full and is already closed — there is nothing left to void.");
        }

        /*
         * Completed payments only. A Pending or Failed payment row is not money the gym holds, and
         * treating it as such would block the void on a card attempt that never went through — the
         * same distinction InvoiceStatusPolicy draws, so the two cannot disagree about whether an
         * invoice has been paid.
         */
        var completedPayments = await db.Payments
            .Where(p => p.InvoiceId == invoice.Id && p.Status == PaymentStatus.Completed)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

        if (completedPayments > 0)
        {
            throw new ValidationException(
                $"Invoice {invoice.InvoiceNumber} cannot be voided because {completedPayments:0.00} {invoice.Currency} " +
                "has already been paid against it. Refund the payment first — that money exists and needs giving back, not erasing.");
        }

        invoice.Status = InvoiceStatus.Cancelled;

        // Appended, never overwritten: whatever the invoice already said about itself (the coupon
        // applied, the auto-renewal it came from) is the context that makes the void reason readable.
        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);
        var entry = $"Voided {today:yyyy-MM-dd}: {request.Reason}";
        invoice.Notes = string.IsNullOrWhiteSpace(invoice.Notes) ? entry : $"{invoice.Notes}{Environment.NewLine}{entry}";

        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
