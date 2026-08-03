using GymOS.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace GymOS.API.Authorization;

public class PermissionAuthorizationHandler(ICurrentUserService currentUser) : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (currentUser.HasPermission(requirement.PermissionCode))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
