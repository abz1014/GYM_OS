using System.Text.Json;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Auditing;
using MediatR;

namespace GymOS.Application.Common.Behaviors;

/// <summary>
/// Closes the workflow's missing "Audit" step (Non-Negotiable Principle #4: "every business
/// action is auditable") — the AuditLog table existed and was fully migrated, but nothing ever
/// wrote to it. Registered after TransactionBehavior so the audit row is written inside the same
/// DB transaction as the business change it records: if the transaction rolls back, so does the
/// audit entry, since an audit log for a change that never happened would be actively misleading.
///
/// Every ICommand is covered automatically with no per-command opt-in, matching Principle #2
/// (never duplicate business logic) — individual handlers don't need to know audit logging exists.
/// </summary>
public class AuditBehavior<TRequest, TResponse>(IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand<TResponse>
{
    private static readonly string[] SensitiveKeywords = ["password", "secret", "token", "code"];

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var response = await next(cancellationToken);

        if (currentUser.TenantId is not null)
        {
            var requestType = typeof(TRequest);

            db.AuditLogs.Add(new AuditLog
            {
                TenantId = currentUser.TenantId.Value,
                UserId = currentUser.UserId,
                Action = requestType.Name,
                EntityType = InferEntityType(requestType),
                EntityId = InferEntityId(request, response),
                DataAfter = SerializeRedacted(request),
                OccurredAt = dateTimeProvider.UtcNow
            });

            await db.SaveChangesAsync(cancellationToken);
        }

        return response;
    }

    /// <summary>The module a command lives in (e.g. "Members", "Maintenance") — coarser than a
    /// precise entity name, but always correct with zero per-command configuration.</summary>
    private static string InferEntityType(Type requestType)
    {
        var segments = requestType.Namespace?.Split('.') ?? [];
        var modulesIndex = Array.IndexOf(segments, "Modules");
        return modulesIndex >= 0 && modulesIndex + 1 < segments.Length ? segments[modulesIndex + 1] : "Unknown";
    }

    private static Guid InferEntityId(TRequest request, TResponse response)
    {
        if (response is Guid responseId)
        {
            return responseId;
        }

        var idProperty = typeof(TRequest).GetProperties()
            .FirstOrDefault(p => p.PropertyType == typeof(Guid) && p.Name.EndsWith("Id", StringComparison.Ordinal));

        return idProperty?.GetValue(request) as Guid? ?? Guid.Empty;
    }

    private static string SerializeRedacted(TRequest request)
    {
        var redacted = new Dictionary<string, object?>();

        foreach (var property in typeof(TRequest).GetProperties())
        {
            var isSensitive = SensitiveKeywords.Any(k => property.Name.Contains(k, StringComparison.OrdinalIgnoreCase));
            redacted[property.Name] = isSensitive ? "***REDACTED***" : property.GetValue(request);
        }

        return JsonSerializer.Serialize(redacted);
    }
}
