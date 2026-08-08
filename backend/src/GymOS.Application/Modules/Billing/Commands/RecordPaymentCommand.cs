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
         */
        var completedPayments = invoice.Payments.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount)
            + request.Amount;

        var completedRefunds = await db.Refunds
            .Where(r => r.Payment != null && r.Payment.InvoiceId == invoice.Id && r.Status == RefundStatus.Completed)
            .SumAsync(r => r.Amount, cancellationToken);

        invoice.Status = InvoiceStatusPolicy.Derive(
            invoice.TotalAmount, completedPayments, completedRefunds,
            invoice.DueDate, DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime));

        await db.SaveChangesAsync(cancellationToken);

        await dashboardNotifier.NotifyBranchActivityAsync(invoice.BranchId, "payment", cancellationToken);

        return payment.Id;
    }
}
