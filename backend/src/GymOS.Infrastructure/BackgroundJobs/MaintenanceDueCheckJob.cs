using GymOS.Domain.Maintenance;
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
public class MaintenanceDueCheckJob(GymOsDbContext db, IDateTimeProvider dateTimeProvider, ILogger<MaintenanceDueCheckJob> logger)
{
    private const string TemplateCode = "maintenance-due";

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);

        var tenantIds = await db.Tenants.IgnoreQueryFilters().Select(t => t.Id).ToListAsync(cancellationToken);

        foreach (var tenantId in tenantIds)
        {
            var template = await db.NotificationTemplates.IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Code == TemplateCode, cancellationToken);

            if (template is null)
            {
                continue;
            }

            // LastNotifiedDueDate (not notification history) drives the dedup check: a schedule's Id
            // never changes, so keying dedup only on Id — as the old logic did — meant a schedule
            // notified once stayed silent forever, even after its NextDueDate later advanced to a
            // new due cycle. Comparing against the current NextDueDate lets each cycle notify once.
            var dueSchedules = await db.MaintenanceSchedules.IgnoreQueryFilters()
                .Include(s => s.Asset)
                .Where(s => s.IsActive && s.NextDueDate <= today && s.LastNotifiedDueDate != s.NextDueDate && s.Asset!.TenantId == tenantId)
                .ToListAsync(cancellationToken);

            foreach (var schedule in dueSchedules)
            {
                var recipientUserIds = await StaffNotificationRecipients.GetUsersWithPermissionInBranchAsync(
                    db, schedule.Asset!.BranchId, PermissionCodes.Maintenance.Manage, cancellationToken);

                foreach (var userId in recipientUserIds)
                {
                    db.ScheduledNotifications.Add(new ScheduledNotification
                    {
                        TenantId = tenantId,
                        BranchId = schedule.Asset.BranchId,
                        NotificationTemplateId = template.Id,
                        RecipientUserId = userId,
                        ScheduledFor = dateTimeProvider.UtcNow,
                        Status = ScheduledNotificationStatus.Pending,
                        RelatedEntityType = nameof(MaintenanceSchedule),
                        RelatedEntityId = schedule.Id
                    });
                }

                // Only mark this cycle notified if someone was actually notified — an empty
                // recipient list (no staff hold Maintenance.Manage in this branch yet) should keep
                // retrying daily rather than silently giving up on the cycle.
                if (recipientUserIds.Count > 0)
                {
                    schedule.LastNotifiedDueDate = schedule.NextDueDate;
                }
            }
        }

        var created = await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Maintenance due check scheduled {Count} notification(s)", created);
        return created;
    }
}
