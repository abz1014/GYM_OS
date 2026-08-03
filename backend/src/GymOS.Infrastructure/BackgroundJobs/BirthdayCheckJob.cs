using GymOS.Application.Common.Interfaces;
using GymOS.Domain.Members;
using GymOS.Domain.Notifications;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GymOS.Infrastructure.BackgroundJobs;

/// <summary>
/// Recurring job (registered daily via Hangfire in Program.cs). Runs across every tenant
/// explicitly with IgnoreQueryFilters() + manual TenantId scoping, matching MembershipExpiryCheckJob.
/// </summary>
public class BirthdayCheckJob(GymOsDbContext db, IDateTimeProvider dateTimeProvider, ILogger<BirthdayCheckJob> logger)
{
    private const string TemplateCode = "birthday";

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

            var birthdayMembers = await db.Members.IgnoreQueryFilters()
                .Where(m => m.TenantId == tenantId && m.DateOfBirth != null
                    && m.DateOfBirth!.Value.Month == today.Month && m.DateOfBirth!.Value.Day == today.Day)
                .Select(m => new { m.Id, m.BranchId })
                .ToListAsync(cancellationToken);

            foreach (var member in birthdayMembers)
            {
                // Deduped per calendar year (not "ever", unlike membership-expiry) since a birthday recurs annually.
                var alreadyScheduled = await db.ScheduledNotifications.IgnoreQueryFilters().AnyAsync(
                    n => n.RelatedEntityType == nameof(Member) && n.RelatedEntityId == member.Id
                        && n.ScheduledFor.Year == today.Year,
                    cancellationToken);

                if (alreadyScheduled)
                {
                    continue;
                }

                db.ScheduledNotifications.Add(new ScheduledNotification
                {
                    TenantId = tenantId,
                    BranchId = member.BranchId,
                    NotificationTemplateId = template.Id,
                    RecipientMemberId = member.Id,
                    ScheduledFor = dateTimeProvider.UtcNow,
                    Status = ScheduledNotificationStatus.Pending,
                    RelatedEntityType = nameof(Member),
                    RelatedEntityId = member.Id
                });
            }
        }

        var created = await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Birthday check scheduled {Count} notification(s)", created);
        return created;
    }
}
