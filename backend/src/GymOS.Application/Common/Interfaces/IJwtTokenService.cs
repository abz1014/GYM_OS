using GymOS.Domain.Identity;

namespace GymOS.Application.Common.Interfaces;

public interface IJwtTokenService
{
    (string Token, DateTimeOffset ExpiresAt) GenerateAccessToken(User user, IReadOnlyList<string> roles);

    string GenerateRefreshTokenValue();
}
