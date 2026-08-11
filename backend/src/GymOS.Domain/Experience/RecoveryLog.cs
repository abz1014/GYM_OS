using GymOS.Domain.Common;

namespace GymOS.Domain.Experience;

/// <summary>
/// A member's logged rest / recovery day — the counterpart to a <c>WorkoutLog</c> on the other side of
/// the training-load ledger. Append-only and member-scoped (no TenantId, like WorkoutLog); the
/// <see cref="RecoveryPolicy"/> reads these as the "rest logged" signal and the Member Experience
/// Engine awards recovery XP once per logged day.
/// </summary>
public class RecoveryLog : AggregateRoot, ITenantScoped
{
    /// <summary>
    /// Direct tenant scoping, so isolation is a property of the schema rather than of every query
    /// that happens to start from Member.
    ///
    /// This table was reachable only through a tenant-scoped Member, which made it safe in practice
    /// and unguarded in principle: one future query beginning here instead of at Member would cross
    /// tenants silently, with nothing failing. Same class of gap as the cross-branch IDOR, same fix —
    /// enforce it in the model so nobody has to remember.
    /// </summary>
    public Guid TenantId { get; set; }

    public Guid MemberId { get; set; }

    /// <summary>The day being rested — the unit the policy and the XP award are keyed on.</summary>
    public DateOnly LoggedOn { get; set; }

    public RecoveryKind Kind { get; set; }

    public string? Notes { get; set; }

    /// <summary>Signals the Member Experience Engine that the member logged recovery on
    /// <see cref="LoggedOn"/>. Called by the command after the log is populated; dispatched after save.</summary>
    public void RaiseLogged() => AddDomainEvent(new RecoveryLoggedEvent(MemberId, LoggedOn));
}
