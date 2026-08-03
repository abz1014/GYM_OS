namespace GymOS.Application.Common.Interfaces;

/// <summary>
/// In-process, per-email failed-login tracking. Deliberately not persisted through the same
/// transactional IApplicationDbContext as the rest of a request: LoginCommand's failure path
/// throws UnauthorizedAccessException, which TransactionBehavior's catch block rolls back —
/// including any DB write that same call just made. Routing the counter through here instead of
/// a User column means the exception that reports "wrong password" can never also erase the
/// attempt it's reporting. Ephemeral by design (a restart clears it), matching this app's
/// single-instance demo deployment scope; a multi-instance production deployment would back this
/// with a shared store (e.g. Redis) instead, with no change needed anywhere else.
/// </summary>
public interface ILoginAttemptTracker
{
    /// <summary>Null if not currently locked, otherwise the UTC instant the lock expires.</summary>
    DateTimeOffset? GetLockedUntil(string email, DateTimeOffset now);

    /// <summary>Records a failed attempt; returns the lock instant if this attempt just triggered one.</summary>
    DateTimeOffset? RecordFailure(string email, DateTimeOffset now);

    void Reset(string email);
}
