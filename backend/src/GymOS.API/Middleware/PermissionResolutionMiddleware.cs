using GymOS.Application.Common.Interfaces;
using GymOS.Application.Modules.Auth;

namespace GymOS.API.Middleware;

/// <summary>
/// Runs once per authenticated request (after UseAuthentication, before UseAuthorization),
/// resolving the caller's permission codes in a single query and stashing them on
/// HttpContext.Items — ICurrentUserService.Permissions reads from there rather than each policy
/// check hitting the database separately.
/// </summary>
public class PermissionResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IApplicationDbContext db, ICurrentUserService currentUser)
    {
        if (currentUser.IsAuthenticated && currentUser.UserId is not null)
        {
            context.Items["Permissions"] =
                await UserContextLoader.GetPermissionCodesAsync(db, currentUser.UserId.Value, context.RequestAborted);

            /*
             * Resolved here, once, for the same reason permissions are: the DbContext's branch query
             * filter needs this value synchronously while building an expression, and it comes from
             * a join. Setting it only for an authenticated caller is deliberate — an absent key
             * leaves AccessibleBranchIds null, which the filter reads as the system context.
             *
             * This runs BEFORE the query filter is ever used, because middleware completes before
             * any handler touches the database on this request.
             */
            context.Items["AccessibleBranchIds"] =
                await UserContextLoader.GetAccessibleBranchIdsAsync(db, currentUser.UserId.Value, context.RequestAborted);
        }

        await next(context);
    }
}
