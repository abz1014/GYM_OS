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
        var payment = await db.Payments
            .Include(p => p.Invoice)
            .FirstOrDefaultAsync(p => p.Id == request.PaymentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Payment), request.PaymentId);

        if (request.Amount > payment.Amount)
        {
            throw new ValidationException("Refund amount cannot exceed the original payment amount.");
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
            payment.Invoice.Status = InvoiceStatus.Refunded;
        }

        await db.SaveChangesAsync(cancellationToken);

        return refund.Id;
    }
}
