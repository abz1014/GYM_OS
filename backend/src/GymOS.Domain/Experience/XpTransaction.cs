using GymOS.Domain.Common;

namespace GymOS.Domain.Experience;

/// <summary>
/// One append-only entry in a member's experience ledger — the blueprint's "XPTransaction", and the
/// set of them for a member is its "ExperienceLedger". Never updated or deleted: the member's level
/// is a projection (<see cref="MemberProgression"/>) that can always be rebuilt by replaying these.
///
/// Not <see cref="IAuditable"/> on purpose: these rows are written by domain-event handlers, which
/// run without an HTTP user in scope, so they carry their own <see cref="OccurredAt"/> rather than
/// relying on the audit interceptor (the same choice LeadActivity.CreatedAt already made).
/// (SourceType, SourceId, Reason) is the idempotency key — the award handler refuses to write a
/// second row with the same triple, so a re-published event never double-credits.
/// </summary>
public class XpTransaction : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid MemberId { get; set; }

    public int Amount { get; set; }

    public XpReason Reason { get; set; }

    public XpSourceType SourceType { get; set; }

    public Guid? SourceId { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}
