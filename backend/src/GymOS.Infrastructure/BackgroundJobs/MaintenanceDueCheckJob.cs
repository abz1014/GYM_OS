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

            var dueSchedules = await db.MaintenanceSchedules.IgnoreQueryFilters()
                .Where(s => s.IsActive && s.NextDueDate <= today && s.Asset!.TenantId == tenantId)
                .Select(s => new { s.Id, s.Asset!.BranchId })
                .ToListAsync(cancellationToken);

            foreach (var schedule in dueSchedules)
            {
                var alreadyScheduled = await db.ScheduledNotifications.IgnoreQueryFilters().AnyAsync(
                    n => n.RelatedEntityType == nameof(MaintenanceSchedule) && n.RelatedEntityId == schedule.Id,
                    cancellationToken);

                if (alreadyScheduled)
                {
                    continue;
                }

                var recipientUserIds = await GetUsersWithPermissionInBranchAsync(schedule.BranchId, PermissionCodes.Maintenance.Manage, cancellationToken);

                foreach (var userId in recipientUserIds)
                {
                    db.ScheduledNotifications.Add(new ScheduledNotification
                    {
                        TenantId = tenantId,
                        BranchId = schedule.BranchId,
                        NotificationTemplateId = template.Id,
                        RecipientUserId = userId,
                        ScheduledFor = dateTimeProvider.UtcNow,
                        Status = ScheduledNotificationStatus.Pending,
                        RelatedEntityType = nameof(MaintenanceSchedule),
                        RelatedEntityId = schedule.Id
                    });
                }
            }
        }

        var created = await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Maintenance due check scheduled {Count} notification(s)", created);
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
