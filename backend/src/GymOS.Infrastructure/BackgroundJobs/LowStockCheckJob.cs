using GymOS.Domain.Inventory;
using GymOS.Domain.Notifications;
using GymOS.Application.Common.Interfaces;
using GymOS.Infrastructure.Persistence;
using GymOS.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GymOS.Infrastructure.BackgroundJobs;

/// <summary>
/// Recurring job (registered daily via Hangfire in Program.cs). Runs across every tenant
/// explicitly with IgnoreQueryFilters() + manual TenantId scoping, matching MembershipExpiryCheckJob.
/// </summary>
public class LowStockCheckJob(GymOsDbContext db, IDateTimeProvider dateTimeProvider, ILogger<LowStockCheckJob> logger)
{
    private const string TemplateCode = "low-stock";

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var tenantIds = await db.Tenants.IgnoreQueryFilters().Select(t => t.Id).ToListAsync(cancellationToken);

        foreach (var tenantId in tenantIds)
        {
            var template = await db.NotificationTemplates.IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Code == TemplateCode, cancellationToken);

            if (template is null)
            {
                continue;
            }

            var lowStockItems = await db.InventoryItems.IgnoreQueryFilters()
                .Where(i => i.TenantId == tenantId && i.QuantityOnHand <= i.ReorderLevel)
                .Select(i => new { i.Id, i.BranchId })
                .ToListAsync(cancellationToken);

            foreach (var item in lowStockItems)
            {
                var alreadyScheduled = await db.ScheduledNotifications.IgnoreQueryFilters().AnyAsync(
                    n => n.RelatedEntityType == nameof(InventoryItem) && n.RelatedEntityId == item.Id,
                    cancellationToken);

                if (alreadyScheduled)
                {
                    continue;
                }

                var recipientUserIds = await GetUsersWithPermissionInBranchAsync(item.BranchId, PermissionCodes.Inventory.Manage, cancellationToken);

                foreach (var userId in recipientUserIds)
                {
                    db.ScheduledNotifications.Add(new ScheduledNotification
                    {
                        TenantId = tenantId,
                        BranchId = item.BranchId,
                        NotificationTemplateId = template.Id,
                        RecipientUserId = userId,
                        ScheduledFor = dateTimeProvider.UtcNow,
                        Status = ScheduledNotificationStatus.Pending,
                        RelatedEntityType = nameof(InventoryItem),
                        RelatedEntityId = item.Id
                    });
                }
            }
        }

        var created = await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Low stock check scheduled {Count} notification(s)", created);
        return created;
    }

    private async Task<List<Guid>> GetUsersWithPermissionInBranchAsync(Guid branchId, string permissionCode, CancellationToken cancellationToken)
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
