using GymOS.Application.Common.Interfaces;

namespace GymOS.Infrastructure.Payments;

/// <summary>Always succeeds with a deterministic fake transaction id — no live gateway required for the MVP demo.</summary>
public class NoOpPaymentGateway : IPaymentGateway
{
    public Task<PaymentGatewayResult> ChargeAsync(decimal amount, string currency, string description, CancellationToken cancellationToken = default)
        => Task.FromResult(new PaymentGatewayResult(true, $"DEMO-TXN-{Guid.NewGuid():N}", null));

    public Task<PaymentGatewayResult> RefundAsync(string gatewayTransactionId, decimal amount, CancellationToken cancellationToken = default)
        => Task.FromResult(new PaymentGatewayResult(true, $"DEMO-REFUND-{Guid.NewGuid():N}", null));
}
