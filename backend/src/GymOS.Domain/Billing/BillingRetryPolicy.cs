namespace GymOS.Domain.Billing;

/// <summary>
/// What to do after a recurring charge is declined. Isolated as a pure function because "how many
/// times do we chase a failed payment, how far apart, and when do we give up" is the rule an owner
/// actually cares about — it belongs in the domain, testable without a gateway or a database, not
/// buried in a background job (same reasoning as ClassBookingPolicy).
/// </summary>
public static class BillingRetryPolicy
{
    /// <summary>Total charge attempts before the membership is suspended (1 initial + 3 retries).</summary>
    public const int MaxAttempts = 4;

    /// <summary>Days to wait before each retry, indexed by how many attempts have already failed.
    /// Escalating backoff (1 → 3 → 5 days) gives a member time to top up a card without letting a
    /// dead card drift unpaid for weeks.</summary>
    private static readonly int[] RetryDelaysInDays = [1, 3, 5];

    /// <summary>True while attempts remain — i.e. the payment should be retried rather than given up on.</summary>
    public static bool ShouldRetry(int failedAttempts) => failedAttempts < MaxAttempts;

    /// <summary>
    /// When the next charge attempt is due after <paramref name="failedAttempts"/> failures, or null
    /// once attempts are exhausted (at which point the caller suspends the membership instead).
    /// </summary>
    public static DateOnly? NextAttemptDate(int failedAttempts, DateOnly lastAttemptDate)
    {
        if (!ShouldRetry(failedAttempts))
        {
            return null;
        }

        // failedAttempts is 1-based here (1 failure → first retry delay).
        var delayIndex = Math.Min(failedAttempts - 1, RetryDelaysInDays.Length - 1);
        return lastAttemptDate.AddDays(RetryDelaysInDays[delayIndex]);
    }

    /// <summary>True when the last attempt has failed and the membership should be suspended.</summary>
    public static bool ShouldSuspend(int failedAttempts) => failedAttempts >= MaxAttempts;
}
