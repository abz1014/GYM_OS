using GymOS.Application.Common.Interfaces;
using GymOS.Application.Modules.Auth.Dtos;
using GymOS.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Auth;

/// <summary>Resolves a User's roles/permissions/branch access — shared by Login, RefreshToken,
/// GetCurrentUser, and the API's per-request PermissionResolutionMiddleware.</summary>
public static class UserContextLoader
{
    public static async Task<CurrentUserDto> BuildAsync(IApplicationDbContext db, User user, CancellationToken cancellationToken)
    {
        // IgnoreQueryFilters: Login runs pre-auth with no ambient tenant, so the Role
        // navigation's tenant-scoped global filter would otherwise silently drop every row.
        var roleNames = await db.UserRoles.IgnoreQueryFilters()
            .Where(ur => ur.UserId == user.Id)
            .Select(ur => ur.Role!.Name)
            .ToListAsync(cancellationToken);

        var permissionCodes = await GetPermissionCodesAsync(db, user.Id, cancellationToken);

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

    /// <summary>
    /// Which branches this user may touch. Shared by BuildAsync (login) and the API's per-request
    /// PermissionResolutionMiddleware, so the set that drives the DB-level branch filter and the set
    /// the client is told about at login can never disagree.
    /// </summary>
    public static Task<List<Guid>> GetAccessibleBranchIdsAsync(IApplicationDbContext db, Guid userId, CancellationToken cancellationToken)
        => db.UserBranchAccesses.Where(a => a.UserId == userId).Select(a => a.BranchId).ToListAsync(cancellationToken);

    public static Task<List<string>> GetRoleNamesAsync(IApplicationDbContext db, Guid userId, CancellationToken cancellationToken)
        => db.UserRoles.IgnoreQueryFilters().Where(ur => ur.UserId == userId).Select(ur => ur.Role!.Name).ToListAsync(cancellationToken);

    /// <summary>The single definition of "which permission codes does this user hold" — if the
    /// grant model ever changes (e.g. per-user grants on top of role grants), this is the only
    /// query to update. UserRole/RolePermission/Permission carry no tenant filter, so this is
    /// safe both pre-auth (Login) and per-request (middleware).</summary>
    public static Task<List<string>> GetPermissionCodesAsync(IApplicationDbContext db, Guid userId, CancellationToken cancellationToken)
        => db.RolePermissions
            .Where(rp => db.UserRoles.Any(ur => ur.UserId == userId && ur.RoleId == rp.RoleId))
            .Select(rp => rp.Permission!.Code)
            .Distinct()
            .ToListAsync(cancellationToken);
}
