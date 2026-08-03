using GymOS.Domain.Common;
using GymOS.Domain.Memberships;

namespace GymOS.Domain.Members;

public class MemberMembership : BaseEntity
{
    public Guid MemberId { get; set; }

    public Member? Member { get; set; }

    public Guid MembershipPlanId { get; set; }

    public MembershipPlan? MembershipPlan { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public MemberMembershipStatus Status { get; set; }

    public bool AutoRenew { get; set; }

    public DateOnly? FreezeStartDate { get; set; }

    public DateOnly? FreezeEndDate { get; set; }

    public decimal PricePaid { get; set; }

    public string Currency { get; set; } = string.Empty;

    /// <summary>The invoice generated for this membership period's fee, so Payment is a real,
    /// trackable step of the signup/renewal workflow rather than just a recorded price.</summary>
    public Guid? InvoiceId { get; set; }

    public string? CancellationReason { get; set; }
}
