using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Infrastructure.BackgroundJobs;

/// <summary>
/// The single definition of "which staff should a background job notify": everyone whose role
/// grants the given permission AND who has access to the given branch. Previously duplicated
/// verbatim in LowStockCheckJob and MaintenanceDueCheckJob — any future recipient-rule change
/// (e.g. excluding inactive users) happens here once.
/// </summary>
internal static class StaffNotificationRecipients
{
    public static async Task<List<Guid>> GetUsersWithPermissionInBranchAsync(
        GymOsDbContext db, Guid branchId, string permissionCode, CancellationToken cancellationToken)
    {
        var roleIdsWithPermission = db.RolePermissions.IgnoreQueryFilters()
            .Where(rp => rp.Permission!.Code == permissionCode)
            .Select(rp => rp.RoleId);

        var userIdsWithRole = db.UserRoles.IgnoreQueryFilters()
            .Where(ur => roleIdsWithPermission.Contains(ur.RoleId))
            .Select(ur => ur.UserId);

        return await db.UserBranchAccesses.IgnoreQueryFilters()
            .Where(uba => uba.BranchId == branchId && userIdsWithRole.Contains(uba.UserId))
            .Select(uba => uba.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}
