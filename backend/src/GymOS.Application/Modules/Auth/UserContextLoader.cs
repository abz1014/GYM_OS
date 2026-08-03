using GymOS.Application.Common.Interfaces;
using GymOS.Application.Modules.Auth.Dtos;
using GymOS.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Auth;

/// <summary>Resolves a User's roles/permissions/branch access — shared by Login, RefreshToken, and GetCurrentUser.</summary>
internal static class UserContextLoader
{
    public static async Task<CurrentUserDto> BuildAsync(IApplicationDbContext db, User user, CancellationToken cancellationToken)
    {
        var roleNames = await db.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Select(ur => ur.Role!.Name)
            .ToListAsync(cancellationToken);

        var permissionCodes = await db.RolePermissions
            .Where(rp => db.UserRoles.Any(ur => ur.UserId == user.Id && ur.RoleId == rp.RoleId))
            .Select(rp => rp.Permission!.Code)
            .Distinct()
            .ToListAsync(cancellationToken);

        var branchIds = await db.UserBranchAccesses
            .Where(a => a.UserId == user.Id)
            .Select(a => a.BranchId)
            .ToListAsync(cancellationToken);

        return new CurrentUserDto(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.MfaEnabled,
            roleNames,
            permissionCodes,
            branchIds);
    }

    public static Task<List<string>> GetRoleNamesAsync(IApplicationDbContext db, Guid userId, CancellationToken cancellationToken)
        => db.UserRoles.Where(ur => ur.UserId == userId).Select(ur => ur.Role!.Name).ToListAsync(cancellationToken);
}
