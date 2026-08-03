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

            // LowStockNotified (not notification history) drives the dedup check — an item's Id
            // never changes, so keying dedup only on Id meant an item notified once stayed silent
            // forever, even after being restocked and dipping low again. RecordStockMovementCommand
            // clears the flag once QuantityOnHand rises back above ReorderLevel.
            var lowStockItems = await db.InventoryItems.IgnoreQueryFilters()
                .Where(i => i.TenantId == tenantId && i.QuantityOnHand <= i.ReorderLevel && !i.LowStockNotified)
                .ToListAsync(cancellationToken);

            foreach (var item in lowStockItems)
            {
                var recipientUserIds = await StaffNotificationRecipients.GetUsersWithPermissionInBranchAsync(
                    db, item.BranchId, PermissionCodes.Inventory.Manage, cancellationToken);

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

                // Only mark notified if someone was actually notified — no staff holding
                // Inventory.Manage in this branch yet should keep retrying daily, not give up.
                if (recipientUserIds.Count > 0)
                {
                    item.LowStockNotified = true;
                }
            }
        }

        var created = await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Low stock check scheduled {Count} notification(s)", created);
        return created;
    }
}
