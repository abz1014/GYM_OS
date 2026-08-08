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
         * The same rule is also what fixes double-submit, which is why there is no separate
         * idempotency key here. A payment is not naturally idempotent the way a check-in is —
         * CheckInCommand can return the existing open visit because "checked in" is a state,
         * whereas paying twice is a thing a person can legitimately do. What is NOT legitimate is
         * paying more than is owed, and a duplicate submit is exactly that: the second copy finds
         * nothing outstanding and is refused. Guarding the real invariant beats pattern-matching on
         * "looks like a duplicate", which would also refuse two genuine £20 cash payments.
         *
         * Checked BEFORE the gateway call below. Reversing those two would charge the member's card
         * and then refuse to record it, which is the worst possible ordering.
         */
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

        if (request.Amount > outstanding)
        {
            throw new ValidationException(
                $"Payment of {request.Amount:0.00} exceeds the {outstanding:0.00} still owed on invoice {invoice.InvoiceNumber}.");
        }

        string? gatewayTransactionId = null;

        if (request.Method == PaymentMethod.Card)
        {
            var result = await paymentGateway.ChargeAsync(request.Amount, invoice.Currency, $"Invoice {invoice.InvoiceNumber}", cancellationToken);

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
            Amount = request.Amount,
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
            invoice.TotalAmount, completedPayments + request.Amount, completedRefunds,
            invoice.DueDate, DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime));

        await db.SaveChangesAsync(cancellationToken);

        await dashboardNotifier.NotifyBranchActivityAsync(invoice.BranchId, "payment", cancellationToken);

        return payment.Id;
    }
}
