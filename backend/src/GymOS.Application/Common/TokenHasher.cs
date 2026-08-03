using System.Security.Cryptography;
using System.Text;

namespace GymOS.Application.Common;

/// <summary>
/// Fast SHA-256 hashing for high-entropy random tokens (refresh tokens, password-reset tokens).
/// Deliberately not BCrypt/Argon2 here — those slow adaptive hashes exist to resist brute-forcing
/// low-entropy human passwords; a 256-bit random token is already infeasible to guess regardless
/// of hash speed, so a fast hash is the right tool and avoids needless latency on every refresh.
/// </summary>
public static class TokenHasher
{
    public static string Hash(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes);
    }

    public static string GenerateRawToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToHexString(bytes);
    }
}
