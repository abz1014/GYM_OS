namespace GymOS.Domain.Billing;

public enum RecurringBillingStatus
{
    /// <summary>Awaiting its next charge attempt (either the first one, or a scheduled retry).</summary>
    Pending,

    /// <summary>Charged successfully — the membership was renewed.</summary>
    Succeeded,

    /// <summary>Every attempt failed; the membership was suspended and needs staff/member action.</summary>
    Abandoned,

    /// <summary>Staff intervened (manual payment, cancellation) so the automated chase stopped.</summary>
    Cancelled
}
