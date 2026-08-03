using Microsoft.AspNetCore.Authorization;

namespace GymOS.API.Authorization;

public class PermissionRequirement(string permissionCode) : IAuthorizationRequirement
{
    public string PermissionCode { get; } = permissionCode;
}
