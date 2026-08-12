using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Billing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Billing.Commands;

public record IssueRefundCommand(Guid PaymentId, decimal Amount, string Reason) : ICommand<Guid>;

public class IssueRefundCommandValidator : AbstractValidator<IssueRefundCommand>
{
    public IssueRefundCommandValidator()
    {
        RuleFor(x => x.PaymentId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty();
    }
}

public class IssueRefundCommandHandler(
    IApplicationDbContext db,
    IPaymentGateway paymentGateway,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<IssueRefundCommand, Guid>
{
    public async Task<Guid> Handle(IssueRefundCommand request, CancellationToken cancellationToken)
    {
        /*
         * The invoice behind this payment is located first and locked, before any figure that
         * decides the outcome is read.
         *
         * Eight concurrent refunds of a $100 payment previously all succeeded, taking $800 back out
         * of it — the same unguarded read-then-write as the payment path, and the more expensive
         * direction to get wrong, because this one moves money outward.
         *
         * The INVOICE is the lock target even though this ceiling is per-payment. That is
         * deliberate: RecordPaymentCommand's ceiling reads refunds, so payments and refunds have to
         * serialise against each other, and giving the pair a single lock target means there is no
         * lock ordering to get wrong and so no deadlock to reason about. This first read is
         * unlocked and is used only to find the invoice id — every figure the decision rests on is
         * re-read below, after the lock is held.
         */
        var invoiceId = await db.Payments
            .Where(p => p.Id == request.PaymentId)
            .Select(p => p.InvoiceId)
            .FirstOrDefaultAsync(cancellationToken);

        if (invoiceId != Guid.Empty)
        {
            await db.LockInvoiceForUpdateAsync(invoiceId, cancellationToken);
        }

        var payment = await db.Payments
            .Include(p => p.Invoice)
            .FirstOrDefaultAsync(p => p.Id == request.PaymentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Payment), request.PaymentId);

        /*
         * Against everything already refunded, not just against the payment.
         *
         * The old check compared a single request to `payment.Amount`, which let the same payment be
         * refunded repeatedly: refunding $10 twice on a $10 payment returned 200 both times and left
         * the invoice showing more refunded than was ever taken. The ceiling is the payment total
         * minus what has already gone back.
         */
        var alreadyRefunded = await db.Refunds
            .Where(r => r.PaymentId == payment.Id && r.Status == RefundStatus.Completed)
            .SumAsync(r => r.Amount, cancellationToken);

        var refundable = payment.Amount - alreadyRefunded;

        if (request.Amount > refundable)
        {
            throw new ValidationException(alreadyRefunded > 0
                ? $"Only {refundable:0.00} of this {payment.Amount:0.00} payment is still refundable; {alreadyRefunded:0.00} has already been refunded."
                : "Refund amount cannot exceed the original payment amount.");
        }

        if (payment.GatewayTransactionId is not null)
        {
            var result = await paymentGateway.RefundAsync(payment.GatewayTransactionId, request.Amount, cancellationToken);
            if (!result.Success)
            {
                throw new ValidationException($"Refund failed: {result.ErrorMessage}");
            }
        }

        var refund = new Refund
        {
            PaymentId = payment.Id,
            Amount = request.Amount,
            Reason = request.Reason,
            ApprovedByUserId = currentUser.UserId,
            RefundedAt = dateTimeProvider.UtcNow,
            Status = RefundStatus.Completed
        };

        db.Refunds.Add(refund);

        if (payment.Invoice is not null)
        {
            /*
             * Derived, not assumed. This line used to read `Status = InvoiceStatus.Refunded`
             * unconditionally, which is the whole bug: a $0.01 refund on a $130 invoice marked it
             * Refunded, and because the nightly overdue job only reconsiders Issued and
             * PartiallyPaid, it could never come back. The invoice left every queue while the
             * balance was still owed and no role had a transition to retrieve it.
             *
             * Now a refund recomputes the invoice from what is actually paid and owed, so it only
             * lands on Refunded when refunds cover the invoice — see InvoiceStatusPolicy.
             */
            var invoice = payment.Invoice;

            var completedPayments = await db.Payments
                .Where(p => p.InvoiceId == invoice.Id && p.Status == PaymentStatus.Completed)
                .SumAsync(p => p.Amount, cancellationToken);

            // The refund being written is not in the database yet, so it is added by hand.
            var completedRefunds = await db.Refunds
                .Where(r => r.Payment != null && r.Payment.InvoiceId == invoice.Id && r.Status == RefundStatus.Completed)
                .SumAsync(r => r.Amount, cancellationToken) + request.Amount;

            invoice.Status = InvoiceStatusPolicy.Derive(
                invoice.TotalAmount, completedPayments, completedRefunds,
                invoice.DueDate, DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime));
        }

        await db.SaveChangesAsync(cancellationToken);

        return refund.Id;
    }
}
