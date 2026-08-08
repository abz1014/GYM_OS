namespace GymOS.Application.Common.Exceptions;

/// <summary>
/// The caller is allowed to do this, and has done it too often.
///
/// Distinct from ForbiddenAccessException on purpose: forbidden means "not yours" and never becomes
/// true by waiting, while this is a door that opens again on its own. It maps to 429 rather than
/// 400, because a client that retries a validation error is broken and a client that retries this
/// after a pause is behaving correctly — the status code is the only thing that tells them apart.
/// </summary>
public class RateLimitExceededException(string message) : Exception(message);
