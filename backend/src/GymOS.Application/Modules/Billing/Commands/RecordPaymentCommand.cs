using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Billing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Billing.Commands;

public record RecordPaymentCommand(Guid InvoiceId, PaymentMethod Method, decimal Amount) : ICommand<Guid>;

public class RecordPaymentCommandValidator : AbstractValidator<RecordPaymentCommand>
{
    public RecordPaymentCommandValidator()
    {
        RuleFor(x => x.InvoiceId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}

public class RecordPaymentCommandHandler(
    IApplicationDbContext db,
    IPaymentGateway paymentGateway,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    IDashboardNotifier dashboardNotifier) : IRequestHandler<RecordPaymentCommand, Guid>
{
    public async Task<Guid> Handle(RecordPaymentCommand request, CancellationToken cancellationToken)
    {
        /*
         * Lock FIRST, before a single figure is read.
         *
         * The ceiling below is a read-then-write, and until this line existed it was not one
         * operation: six concurrent full payments against a $100 invoice were all accepted, leaving
         * $600 recorded and the balance at -$500. Every one of them read the same untouched balance
         * and every one of them passed. A lock taken after the read would protect nothing, which is
         * why it is the first statement in the handler rather than sitting next to the check it
         * guards.
         */
        await db.LockInvoiceForUpdateAsync(request.InvoiceId, cancellationToken);

        var invoice = await db.Invoices
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken)
            ?? throw new NotFoundException(nameof(Invoice), request.InvoiceId);

        /*
         * A payment may not exceed what is still owed.
         *
         * The validator only checked `Amount > 0`, so a $1,000,000,000,000 payment on a $49.99
         * invoice was accepted with 200 and drove the dashboard's revenue tile to twelve figures.
         *
         * The same rule is also what handles double-submit, which is why there is no separate
         * idempotency key here. A payment is not naturally idempotent the way a check-in is —
         * CheckInCommand can return the existing open visit because "checked in" is a state,
         * whereas paying twice is a thing a person can legitimately do. What is NOT legitimate is
         * paying more than is owed, and a duplicate submit is exactly that: the second copy finds
         * nothing outstanding and is refused. Guarding the real invariant beats pattern-matching on
         * "looks like a duplicate", which would also refuse two genuine £20 cash payments.
         *
         * That argument is only sound because of the lock above, and for a while it was not. A
         * double-submit is two requests a few milliseconds apart — the one shape an unlocked
         * read-then-write cannot see, because the second copy reads the balance before the first has
         * written. The rule was right and the implementation could not enforce it.
         *
         * Checked BEFORE the gateway call below. Reversing those two would charge the member's card
         * and then refuse to record it, which is the worst possible ordering.
         */
        /*
         * Rounded once, here, and used for the check, the stored row and the derived status alike.
         *
         * Payment.Amount persists as numeric(18,2) (GymOsDbContext's decimal convention), so an
         * amount carrying more scale than that is one number when it is checked and a different one
         * once it is stored. Paying 99.999 against a $100 invoice stored 100.00 — settling it — while
         * the status was derived from 99.999 and came out PartiallyPaid: an invoice showing $0.00
         * outstanding that no further payment could ever close, because the ceiling correctly refused
         * to add to a balance of zero.
         */
        var amount = Math.Round(request.Amount, 2, MidpointRounding.ToEven);

        var completedPayments = invoice.Payments.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount);

        var completedRefunds = await db.Refunds
            .Where(r => r.Payment != null && r.Payment.InvoiceId == invoice.Id && r.Status == RefundStatus.Completed)
            .SumAsync(r => r.Amount, cancellationToken);

        var outstanding = InvoiceStatusPolicy.Outstanding(invoice.TotalAmount, completedPayments, completedRefunds);

        if (outstanding <= 0)
        {
            throw new ValidationException(
                $"Invoice {invoice.InvoiceNumber} is already settled — there is nothing left to pay.");
        }

        if (amount > outstanding)
        {
            throw new ValidationException(
                $"Payment of {amount:0.00} exceeds the {outstanding:0.00} still owed on invoice {invoice.InvoiceNumber}.");
        }

        string? gatewayTransactionId = null;

        if (request.Method == PaymentMethod.Card)
        {
            var result = await paymentGateway.ChargeAsync(amount, invoice.Currency, $"Invoice {invoice.InvoiceNumber}", cancellationToken);

            if (!result.Success)
            {
                throw new ValidationException($"Card payment failed: {result.ErrorMessage}");
            }

            gatewayTransactionId = result.TransactionId;
        }

        var payment = new Payment
        {
            InvoiceId = invoice.Id,
            Method = request.Method,
            Amount = amount,
            PaidAt = dateTimeProvider.UtcNow,
            ReceivedByUserId = currentUser.UserId,
            GatewayTransactionId = gatewayTransactionId,
            Status = PaymentStatus.Completed
        };

        db.Payments.Add(payment);

        /*
         * Same derivation the refund path uses, for two reasons.
         *
         * Refunds were invisible here: paying $40 against an invoice whose earlier $40 payment had
         * been refunded produced Paid, because the sum ignored the money that went back. And the old
         * line could not produce Overdue at all, so a part-payment on a long-overdue invoice quietly
         * moved it to PartiallyPaid and out of the collections queue — the same disappearing act the
         * refund bug performed, by a different route.
         *
         * Reuses the totals the ceiling above already computed — re-querying them here would mean two
         * answers to "what has been paid" in one method, which is how they drift apart.
         */
        invoice.Status = InvoiceStatusPolicy.Derive(
            invoice.TotalAmount, completedPayments + amount, completedRefunds,
            invoice.DueDate, DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime));

        await db.SaveChangesAsync(cancellationToken);

        await dashboardNotifier.NotifyBranchActivityAsync(invoice.BranchId, "payment", cancellationToken);

        return payment.Id;
    }
}
