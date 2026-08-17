using FluentValidation;
using GymOS.Application.Common.Interfaces;
using GymOS.Domain.Identity;
using GymOS.Shared;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Settings.Commands;

/// <summary>
/// The checks create-staff and update-staff must agree on. Shared rather than copied because they are
/// the two halves of the same decision — if editing accepted a role or a branch that hiring rejects,
/// the rule would be enforceable only until someone opened the edit dialog.
/// </summary>
internal static class StaffAccountGuards
{
    /// <summary>
    /// Resolves a role name to a role in the caller's own tenant, refusing Member.
    ///
    /// Member is refused because it is not a job. A Member row is what links a login to a person's
    /// membership, dues and attendance, and the staff screen creates none of that — a "staff member"
    /// given the Member role would get the self-service portal pointed at a member record that does
    /// not exist, and would appear on no screen able to repair it.
    /// </summary>
    public static async Task<Role> ResolveAssignableRoleAsync(
        IApplicationDbContext db, Guid tenantId, string roleName, CancellationToken cancellationToken)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Name == roleName, cancellationToken)
            ?? throw new ValidationException($"Role \"{roleName}\" does not exist.");

        if (role.Name == RoleNames.Member)
        {
            throw new ValidationException("The Member role cannot be assigned to a staff account.");
        }

        return role;
    }

    /// <summary>
    /// Narrows the requested branch ids to ones that are actually this gym's. db.Branches is
    /// tenant-filtered, so a branch id belonging to another tenant is simply not found and reads back
    /// as "not one of yours" — which is what the caller should be told, rather than being handed an
    /// account with access to a stranger's site.
    /// </summary>
    public static async Task<List<Guid>> ResolveBranchIdsAsync(
        IApplicationDbContext db, IReadOnlyList<Guid> requestedBranchIds, CancellationToken cancellationToken)
    {
        var distinctIds = requestedBranchIds.Distinct().ToList();

        var knownIds = await db.Branches
            .Where(b => distinctIds.Contains(b.Id))
            .Select(b => b.Id)
            .ToListAsync(cancellationToken);

        var unknownId = distinctIds.Except(knownIds).FirstOrDefault();
        if (unknownId != Guid.Empty)
        {
            throw new ValidationException($"Branch \"{unknownId}\" is not a branch of this gym.");
        }

        return distinctIds;
    }

    /// <summary>
    /// True when <paramref name="userId"/> is the only active account left in the tenant whose role
    /// grants settings.manage_staff.
    ///
    /// Deactivating that account is a one-way door. LoginCommand refuses inactive users, there is no
    /// super-admin console above the tenant, and the only screen that could switch someone back on is
    /// the one this permission gates — so the gym would be left with a product nobody can hire into,
    /// recoverable only by a developer with database access.
    /// </summary>
    public static async Task<bool> IsLastActiveStaffManagerAsync(
        IApplicationDbContext db, Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        var permissionId = await db.Permissions
            .Where(p => p.Code == PermissionCodes.Settings.ManageStaff)
            .Select(p => p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (permissionId == Guid.Empty)
        {
            return false;
        }

        // Roles, not the caller's own permission list: the question is who *could* hire tomorrow, and
        // grants are edited live through the permission matrix.
        var grantingRoleIds = await db.Roles
            .Where(r => r.TenantId == tenantId)
            .Where(r => db.RolePermissions.Any(rp => rp.RoleId == r.Id && rp.PermissionId == permissionId))
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        if (grantingRoleIds.Count == 0)
        {
            return false;
        }

        var holderUserIds = await db.UserRoles
            .Where(ur => grantingRoleIds.Contains(ur.RoleId))
            .Select(ur => ur.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var activeHolderIds = await db.Users
            .Where(u => holderUserIds.Contains(u.Id) && u.IsActive)
            .Select(u => u.Id)
            .Distinct()
            .ToListAsync(cancellationToken);

        return activeHolderIds.Count == 1 && activeHolderIds[0] == userId;
    }
}
