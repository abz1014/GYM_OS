using GymOS.Domain.Common;
using GymOS.Domain.Members;

namespace GymOS.Domain.Billing;

/// <summary>
/// The dunning record for one membership's auto-renewal: how many times the card has been charged
/// and failed, when the next attempt is due, and why the last one failed. Separate from Invoice
/// because an invoice is the money owed, whereas this is the *chase* — it survives across retries
/// and is what turns "the payment bounced" into an automated sequence instead of a manual phone
/// call. One live row per membership; resolved (paid or given up) rows stay for history.
/// </summary>
public class RecurringBillingAttempt : BaseEntity, IBranchScoped
{
    public Guid TenantId { get; set; }

    public Guid BranchId { get; set; }

    public Guid MemberMembershipId { get; set; }

    public MemberMembership? MemberMembership { get; set; }

    public Guid MemberId { get; set; }

    public Member? Member { get; set; }

    /// <summary>The invoice raised for this renewal period — the thing being collected.</summary>
    public Guid InvoiceId { get; set; }

    public Invoice? Invoice { get; set; }

    public RecurringBillingStatus Status { get; set; } = RecurringBillingStatus.Pending;

    public int FailedAttempts { get; set; }

    public DateOnly NextAttemptDate { get; set; }

    public DateOnly? LastAttemptDate { get; set; }

    public string? LastFailureReason { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;
}
