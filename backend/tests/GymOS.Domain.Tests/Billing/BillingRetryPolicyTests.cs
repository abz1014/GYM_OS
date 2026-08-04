using GymOS.Domain.Billing;
using Shouldly;

namespace GymOS.Domain.Tests.Billing;

/// <summary>
/// The dunning rule decides how long a gym chases a bounced membership payment before suspending
/// access — a policy an owner would want to reason about directly, so it's pure and tested here
/// rather than implied by the background job's control flow.
/// </summary>
public class BillingRetryPolicyTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    [InlineData(5, false)]
    public void Retries_until_the_attempt_budget_is_spent(int failedAttempts, bool shouldRetry)
    {
        BillingRetryPolicy.ShouldRetry(failedAttempts).ShouldBe(shouldRetry);
        BillingRetryPolicy.ShouldSuspend(failedAttempts).ShouldBe(!shouldRetry);
    }

    [Fact]
    public void Backs_off_further_after_each_successive_failure()
    {
        var lastAttempt = new DateOnly(2026, 8, 4);

        // 1 → 3 → 5 day gaps, so a member gets a week-plus to fix a card before losing access.
        BillingRetryPolicy.NextAttemptDate(1, lastAttempt).ShouldBe(new DateOnly(2026, 8, 5));
        BillingRetryPolicy.NextAttemptDate(2, lastAttempt).ShouldBe(new DateOnly(2026, 8, 7));
        BillingRetryPolicy.NextAttemptDate(3, lastAttempt).ShouldBe(new DateOnly(2026, 8, 9));
    }

    [Fact]
    public void Stops_scheduling_once_attempts_are_exhausted()
    {
        BillingRetryPolicy.NextAttemptDate(BillingRetryPolicy.MaxAttempts, new DateOnly(2026, 8, 4)).ShouldBeNull();
    }
}
