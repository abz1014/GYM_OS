namespace GymOS.Application.Common.Interfaces;

/// <summary>
/// Abstraction over the payment processor. The MVP registers a demo implementation that always
/// succeeds with a deterministic fake transaction id; Stripe/Mollie/SEPA Direct Debit plug in
/// later behind this same interface via appsettings config, with no change to calling code.
/// </summary>
public interface IPaymentGateway
{
    Task<PaymentGatewayResult> ChargeAsync(decimal amount, string currency, string description, CancellationToken cancellationToken = default);

    Task<PaymentGatewayResult> RefundAsync(string gatewayTransactionId, decimal amount, CancellationToken cancellationToken = default);
}

public record PaymentGatewayResult(bool Success, string? TransactionId, string? ErrorMessage);
