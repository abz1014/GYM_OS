using GymOS.Application.Common.Auditing;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
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
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var response = await next(cancellationToken);

        // [AllowAnonymous] commands (Login, Logout, RefreshToken, ForgotPassword, ResetPassword)
        // run before any JWT exists, so TenantId is always null here — they audit themselves
        // directly via AuditLogWriter instead, once they've resolved a user and its TenantId.
        if (currentUser.TenantId is not null)
        {
            var requestType = typeof(TRequest);

            await AuditLogWriter.WriteAsync(
                db, dateTimeProvider,
                currentUser.TenantId.Value, currentUser.UserId,
                requestType.Name, InferEntityType(requestType), InferEntityId(request, response),
                request, cancellationToken);
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
}
