using GymOS.Application.Common.Interfaces;

namespace GymOS.Application.Tests.TestSupport;

/// <summary>
/// Stands in for the card processor, and counts.
///
/// The count is the point. Several rules in the billing handlers are about ORDER rather than
/// outcome — an overpayment must be refused BEFORE the card is charged, because the alternative
/// takes the member's money and then declines to record it. An assertion on the thrown exception
/// alone cannot tell those two orderings apart; <see cref="ChargeCount"/> can.
/// </summary>
public class FakePaymentGateway : IPaymentGateway
{
    /// <summary>Set false to make the processor decline, the way a real one does.</summary>
    public bool Succeeds { get; set; } = true;

    /// <summary>How many times money was actually taken. Zero is a meaningful assertion.</summary>
    public int ChargeCount { get; private set; }

    public int RefundCount { get; private set; }

    public Task<PaymentGatewayResult> ChargeAsync(
        decimal amount, string currency, string description, CancellationToken cancellationToken = default)
    {
        ChargeCount++;
        return Task.FromResult(Succeeds
            ? new PaymentGatewayResult(true, $"TEST-{Guid.NewGuid():N}", null)
            : new PaymentGatewayResult(false, null, "Card declined."));
    }

    public Task<PaymentGatewayResult> RefundAsync(
        string gatewayTransactionId, decimal amount, CancellationToken cancellationToken = default)
    {
        RefundCount++;
        return Task.FromResult(Succeeds
            ? new PaymentGatewayResult(true, $"TEST-REFUND-{Guid.NewGuid():N}", null)
            : new PaymentGatewayResult(false, null, "Refund declined."));
    }
}
