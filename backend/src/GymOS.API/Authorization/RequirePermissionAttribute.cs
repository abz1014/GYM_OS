using Microsoft.AspNetCore.Authorization;

namespace GymOS.API.Authorization;

/// <summary>Thin readability wrapper over [Authorize(Policy = "...")] — the policy name IS the permission code, registered 1:1 in Program.cs from the PermissionCodes catalog.</summary>
public class RequirePermissionAttribute(string permissionCode) : AuthorizeAttribute(permissionCode);
