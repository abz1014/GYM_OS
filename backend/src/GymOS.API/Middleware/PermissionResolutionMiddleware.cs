using GymOS.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

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
            var permissions = await db.RolePermissions.AsNoTracking()
                .Where(rp => db.UserRoles.Any(ur => ur.UserId == currentUser.UserId && ur.RoleId == rp.RoleId))
                .Select(rp => rp.Permission!.Code)
                .Distinct()
                .ToListAsync(context.RequestAborted);

            context.Items["Permissions"] = permissions;
        }

        await next(context);
    }
}
