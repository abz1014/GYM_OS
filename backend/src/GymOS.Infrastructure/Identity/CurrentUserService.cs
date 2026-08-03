using System.Security.Claims;
using GymOS.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace GymOS.Infrastructure.Identity;

/// <summary>
/// Reads UserId/TenantId/Email/Roles straight from JWT claims (cheap, no DB hit). Permissions are
/// the exception — those come from HttpContext.Items, populated once per request by
/// PermissionResolutionMiddleware (API layer) after a single DB query, since resolving the
/// role-&gt;permission join on every ICurrentUserService.Permissions access would mean a property
/// getter doing async DB work, which doesn't fit the sync interface.
/// </summary>
public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public Guid? UserId => Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? User?.FindFirstValue("sub"), out var id) ? id : null;

    public Guid? TenantId => Guid.TryParse(User?.FindFirstValue("tenant_id"), out var id) ? id : null;

    public Guid? BranchId => Guid.TryParse(httpContextAccessor.HttpContext?.Request.Headers["X-Branch-Id"].FirstOrDefault(), out var id) ? id : null;

    public string? Email => User?.FindFirstValue(ClaimTypes.Email);

    public IReadOnlyList<string> Roles => User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? [];

    public IReadOnlyList<string> Permissions => httpContextAccessor.HttpContext?.Items["Permissions"] as List<string> ?? [];

    public bool HasPermission(string permissionCode) => Permissions.Contains(permissionCode);
}
