using System.Text.Json;
using GymOS.Application.Common.Interfaces;
using GymOS.Domain.Auditing;

namespace GymOS.Application.Common.Auditing;

/// <summary>
/// Shared by AuditBehavior (the automatic path, driven by ICurrentUserService.TenantId from the
/// JWT) and the handful of [AllowAnonymous] Auth handlers that must call it directly — Login,
/// Logout, RefreshToken, ForgotPassword, and ResetPassword all run before any JWT exists, so
/// ICurrentUserService.TenantId is null for them and the pipeline behavior alone can never audit
/// them. Those handlers resolve a Guid TenantId themselves (from the user they just looked up) and
/// call this directly, so the same redaction and shape rules apply either way.
/// </summary>
public static class AuditLogWriter
{
    private static readonly string[] SensitiveKeywords = ["password", "secret", "token", "code"];

    /// <summary>
    /// Business identifiers that contain a sensitive keyword as a substring but carry no secret.
    /// Without this, the keyword rule redacted them permanently — e.g. every RenewMembershipCommand
    /// recorded "CouponCode":"***REDACTED***", so the audit trail could never answer which promotion
    /// drove a signup. Redaction stays deny-by-default: a new property matching a keyword is still
    /// redacted unless it is deliberately listed here.
    /// </summary>
    private static readonly HashSet<string> NonSensitiveExceptions =
        new(StringComparer.OrdinalIgnoreCase) { "CouponCode", "MemberCode", "TemplateCode" };

    public static async Task WriteAsync(
        IApplicationDbContext db, IDateTimeProvider dateTimeProvider,
        Guid tenantId, Guid? userId, string action, string entityType, Guid entityId, object request,
        CancellationToken cancellationToken)
    {
        db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenantId,
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            DataAfter = SerializeRedacted(request),
            OccurredAt = dateTimeProvider.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public static string SerializeRedacted(object request)
    {
        var redacted = new Dictionary<string, object?>();

        foreach (var property in request.GetType().GetProperties())
        {
            var isSensitive = !NonSensitiveExceptions.Contains(property.Name)
                && SensitiveKeywords.Any(k => property.Name.Contains(k, StringComparison.OrdinalIgnoreCase));
            redacted[property.Name] = isSensitive ? "***REDACTED***" : property.GetValue(request);
        }

        return JsonSerializer.Serialize(redacted);
    }
}
