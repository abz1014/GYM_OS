using System.Security.Cryptography;
using System.Text;

namespace GymOS.Application.Common;

/// <summary>
/// Turns a stable string key into a stable Guid, so an idempotency key that is naturally a string
/// (e.g. "member:nutrition:2026-08-04") can be stored in a Guid column and always hash to the same
/// value. MD5 is used purely as a fast, deterministic 128-bit hash here — no security is implied.
/// </summary>
public static class DeterministicGuid
{
    public static Guid From(string input) => new(MD5.HashData(Encoding.UTF8.GetBytes(input)));
}
