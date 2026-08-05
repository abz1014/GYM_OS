namespace GymOS.Domain.Experience;

/// <summary>What a member did on a logged recovery day. All count equally toward the "rest logged"
/// signal the <see cref="RecoveryPolicy"/> reads and the once-per-day recovery XP award — the kind is
/// for the member's own record, not a different reward.</summary>
public enum RecoveryKind
{
    RestDay,
    ActiveRecovery,
    Mobility,
    Stretching
}
