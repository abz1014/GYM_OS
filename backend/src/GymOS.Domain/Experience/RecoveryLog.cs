using GymOS.Domain.Common;

namespace GymOS.Domain.Experience;

/// <summary>
/// A member's logged rest / recovery day — the counterpart to a <c>WorkoutLog</c> on the other side of
/// the training-load ledger. Append-only and member-scoped (no TenantId, like WorkoutLog); the
/// <see cref="RecoveryPolicy"/> reads these as the "rest logged" signal and the Member Experience
/// Engine awards recovery XP once per logged day.
/// </summary>
public class RecoveryLog : AggregateRoot
{
    public Guid MemberId { get; set; }

    /// <summary>The day being rested — the unit the policy and the XP award are keyed on.</summary>
    public DateOnly LoggedOn { get; set; }

    public RecoveryKind Kind { get; set; }

    public string? Notes { get; set; }

    /// <summary>Signals the Member Experience Engine that the member logged recovery on
    /// <see cref="LoggedOn"/>. Called by the command after the log is populated; dispatched after save.</summary>
    public void RaiseLogged() => AddDomainEvent(new RecoveryLoggedEvent(MemberId, LoggedOn));
}
