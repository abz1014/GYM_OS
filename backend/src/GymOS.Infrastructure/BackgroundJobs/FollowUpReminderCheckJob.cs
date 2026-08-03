using GymOS.Application.Common.Interfaces;
using GymOS.Domain.Crm;
using GymOS.Domain.Notifications;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GymOS.Infrastructure.BackgroundJobs;

/// <summary>
/// Recurring job (registered daily via Hangfire in Program.cs). Runs across every tenant
/// explicitly with IgnoreQueryFilters() + manual TenantId scoping, matching MembershipExpiryCheckJob.
/// </summary>
public class FollowUpReminderCheckJob(GymOsDbContext db, IDateTimeProvider dateTimeProvider, ILogger<FollowUpReminderCheckJob> logger)
{
    private const string TemplateCode = "follow-up-reminder";

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var now = dateTimeProvider.UtcNow;

        var tenantIds = await db.Tenants.IgnoreQueryFilters().Select(t => t.Id).ToListAsync(cancellationToken);

        foreach (var tenantId in tenantIds)
        {
            var template = await db.NotificationTemplates.IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Code == TemplateCode, cancellationToken);

            if (template is null)
            {
                continue;
            }

            var dueActivities = await db.LeadActivities.IgnoreQueryFilters()
                .Where(a => a.CompletedAt == null && a.DueDate != null && a.DueDate <= now
                    && a.Lead!.TenantId == tenantId && a.Lead!.AssignedToUserId != null)
                .Select(a => new { a.Id, a.Lead!.BranchId, AssignedToUserId = a.Lead!.AssignedToUserId!.Value })
                .ToListAsync(cancellationToken);

            foreach (var activity in dueActivities)
            {
                var alreadyScheduled = await db.ScheduledNotifications.IgnoreQueryFilters().AnyAsync(
                    n => n.RelatedEntityType == nameof(LeadActivity) && n.RelatedEntityId == activity.Id,
                    cancellationToken);

                if (alreadyScheduled)
                {
                    continue;
                }

                db.ScheduledNotifications.Add(new ScheduledNotification
                {
                    TenantId = tenantId,
                    BranchId = activity.BranchId,
                    NotificationTemplateId = template.Id,
                    RecipientUserId = activity.AssignedToUserId,
                    ScheduledFor = now,
                    Status = ScheduledNotificationStatus.Pending,
                    RelatedEntityType = nameof(LeadActivity),
                    RelatedEntityId = activity.Id
                });
            }
        }

        var created = await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Follow-up reminder check scheduled {Count} notification(s)", created);
        return created;
    }
}
