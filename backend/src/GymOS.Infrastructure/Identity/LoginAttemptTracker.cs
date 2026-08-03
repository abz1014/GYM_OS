using System.Collections.Concurrent;
using GymOS.Application.Common.Interfaces;

namespace GymOS.Infrastructure.Identity;

public class LoginAttemptTracker : ILoginAttemptTracker
{
    private const int MaxFailedAttempts = 5;
    private const int LockoutMinutes = 15;

    private sealed record State(int FailedAttempts, DateTimeOffset? LockedUntil);

    private readonly ConcurrentDictionary<string, State> _state = new(StringComparer.OrdinalIgnoreCase);

    public DateTimeOffset? GetLockedUntil(string email, DateTimeOffset now) =>
        _state.TryGetValue(email, out var state) && state.LockedUntil > now ? state.LockedUntil : null;

    public DateTimeOffset? RecordFailure(string email, DateTimeOffset now)
    {
        DateTimeOffset? triggeredLockUntil = null;

        _state.AddOrUpdate(
            email,
            _ => new State(1, null),
            (_, existing) =>
            {
                var attempts = existing.FailedAttempts + 1;
                if (attempts >= MaxFailedAttempts)
                {
                    triggeredLockUntil = now.AddMinutes(LockoutMinutes);
                    return new State(0, triggeredLockUntil);
                }

                return new State(attempts, existing.LockedUntil);
            });

        return triggeredLockUntil;
    }

    public void Reset(string email) => _state.TryRemove(email, out _);
}
