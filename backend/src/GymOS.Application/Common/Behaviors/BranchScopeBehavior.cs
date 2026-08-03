using System.Reflection;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Security;
using MediatR;

namespace GymOS.Application.Common.Behaviors;

/// <summary>
/// Defense-in-depth for branch isolation, matching TenantScopeBehavior's shape: reflects for a
/// BranchId property (Guid or Guid?) on the request and, if the current user's UserBranchAccess
/// rows don't include it, rejects the request before it ever reaches a handler — for every
/// current and future command/query with a BranchId, with zero per-handler code.
///
/// This closes the "explicitly request a foreign branch" half of the gap found in the security
/// review. It does NOT close the other half — a query handler that silently returns every
/// branch's data when BranchId is omitted — because there is nothing here to validate in that
/// case; each affected list query was fixed individually to restrict to the caller's accessible
/// branches when no explicit BranchId is given (see BranchAccessResolver's XML doc for how the
/// gap was originally confirmed).
/// </summary>
public class BranchScopeBehavior<TRequest, TResponse>(IApplicationDbContext db, ICurrentUserService currentUser)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var branchId = ExtractBranchId(request);

        if (branchId is not null)
        {
            var accessibleBranchIds = await BranchAccessResolver.GetAccessibleBranchIdsAsync(db, currentUser, cancellationToken);

            if (!accessibleBranchIds.Contains(branchId.Value))
            {
                throw new ForbiddenAccessException("You do not have access to this branch.");
            }
        }

        return await next(cancellationToken);
    }

    private static Guid? ExtractBranchId(TRequest request)
    {
        var property = typeof(TRequest).GetProperty("BranchId", BindingFlags.Public | BindingFlags.Instance);
        if (property is null)
        {
            return null;
        }

        // Reflection auto-unwraps a non-null Guid? to a boxed Guid, so this one pattern covers
        // both "Guid BranchId" and "Guid? BranchId" properties; a null Guid? falls through to null.
        return property.GetValue(request) switch
        {
            Guid guid => guid,
            _ => null
        };
    }
}
