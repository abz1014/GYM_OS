using GymOS.Domain.Identity;
using Shouldly;

namespace GymOS.Domain.Tests.Identity;

public class RefreshTokenTests
{
    [Fact]
    public void IsActive_is_true_when_not_revoked_and_not_expired()
    {
        var token = new RefreshToken
        {
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
            RevokedAt = null
        };

        token.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void IsActive_is_false_once_revoked_even_if_not_yet_expired()
    {
        var token = new RefreshToken
        {
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
            RevokedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };

        token.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void IsActive_is_false_once_expired_even_if_never_revoked()
    {
        var token = new RefreshToken
        {
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1),
            RevokedAt = null
        };

        token.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void IsActive_is_false_when_both_revoked_and_expired()
    {
        var token = new RefreshToken
        {
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1),
            RevokedAt = DateTimeOffset.UtcNow.AddDays(-2)
        };

        token.IsActive.ShouldBeFalse();
    }
}
