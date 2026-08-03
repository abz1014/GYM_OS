using GymOS.Application.Common.Interfaces;
using GymOS.Domain.Members;
using GymOS.Domain.Notifications;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GymOS.Infrastructure.BackgroundJobs;

/// <summary>
/// Recurring job (registered daily via Hangfire in Program.cs). Runs across every tenant
/// explicitly with IgnoreQueryFilters() + manual TenantId scoping, since background jobs have no
/// HTTP request/JWT to populate ICurrentUserService's ambient tenant context that the DbContext's
/// global query filter otherwise relies on.
/// </summary>
public class MembershipExpiryCheckJob(GymOsDbContext db, IDateTimeProvider dateTimeProvider, ILogger<MembershipExpiryCheckJob> logger)
{
    private const string TemplateCode = "membership-expiry-7-days";

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);
        var sevenDaysOut = today.AddDays(7);

        var tenantIds = await db.Tenants.IgnoreQueryFilters().Select(t => t.Id).ToListAsync(cancellationToken);

        foreach (var tenantId in tenantIds)
        {
            var template = await db.NotificationTemplates.IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Code == TemplateCode, cancellationToken);

            if (template is null)
            {
                continue;
            }

            var expiring = await db.MemberMemberships.IgnoreQueryFilters()
                .Where(mm => mm.Member!.TenantId == tenantId
                    && mm.Status == MemberMembershipStatus.Active
                    && mm.EndDate >= today && mm.EndDate <= sevenDaysOut)
                .Select(mm => new { mm.Id, mm.MemberId, mm.Member!.BranchId })
                .ToListAsync(cancellationToken);

            foreach (var membership in expiring)
            {
                var alreadyScheduled = await db.ScheduledNotifications.IgnoreQueryFilters().AnyAsync(
                    n => n.RelatedEntityType == nameof(MemberMembership) && n.RelatedEntityId == membership.Id,
                    cancellationToken);

                if (alreadyScheduled)
                {
                    continue;
                }

                db.ScheduledNotifications.Add(new ScheduledNotification
                {
                    TenantId = tenantId,
                    BranchId = membership.BranchId,
                    NotificationTemplateId = template.Id,
                    RecipientMemberId = membership.MemberId,
                    ScheduledFor = dateTimeProvider.UtcNow,
                    Status = ScheduledNotificationStatus.Pending,
                    RelatedEntityType = nameof(MemberMembership),
                    RelatedEntityId = membership.Id
                });
            }
        }

        var created = await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Membership expiry check scheduled {Count} notification(s)", created);
        return created;
    }
}
