using GymOS.Application.Common.Interfaces;
using GymOS.Domain.Crm;
using GymOS.Domain.Notifications;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GymOS.Infrastructure.BackgroundJobs;

/// <summary>
/// The automated nurture for leads nobody has gotten to yet. Selection is delegated entirely to
/// LeadDripPolicy (day-3/7/14 escalation, gated on zero logged activity) so the rule is
/// unit-tested; this job only supplies the facts — days since the lead was created, whether any
/// activity exists, which day-markers have already fired — and writes the scheduled notification.
/// Three templates ("lead-drip-day-3/7/14") map to the three escalation steps; a tenant missing a
/// given template's row simply never sends that step, same graceful-skip behavior as every other
/// notification job in this codebase.
/// </summary>
public class LeadDripJob(GymOsDbContext db, IDateTimeProvider dateTimeProvider, ILogger<LeadDripJob> logger)
{
    private static string TemplateCodeFor(int dripDay) => $"lead-drip-day-{dripDay}";

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var now = dateTimeProvider.UtcNow;
        var tenantIds = await db.Tenants.IgnoreQueryFilters().Select(t => t.Id).ToListAsync(cancellationToken);
        var scheduled = 0;

        foreach (var tenantId in tenantIds)
        {
            var templatesByDay = new Dictionary<int, Guid>();
            foreach (var day in LeadDripPolicy.DripDays)
            {
                var template = await db.NotificationTemplates.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Code == TemplateCodeFor(day), cancellationToken);
                if (template is not null)
                {
                    templatesByDay[day] = template.Id;
                }
            }

            if (templatesByDay.Count == 0)
            {
                continue;
            }

            // Only Lead/FollowUp leads are ever eligible (per the policy), so filter in SQL before
            // pulling per-lead activity/notification history — keeps the scan proportional to the
            // open pipeline, not every lead ever created.
            var candidates = await db.Leads.IgnoreQueryFilters()
                .Where(l => l.TenantId == tenantId && (l.Stage == LeadStage.Lead || l.Stage == LeadStage.FollowUp))
                .Select(l => new
                {
                    l.Id,
                    l.BranchId,
                    l.Stage,
                    l.CreatedAt,
                    HasAnyActivity = db.LeadActivities.IgnoreQueryFilters().Any(a => a.LeadId == l.Id)
                })
                .ToListAsync(cancellationToken);

            foreach (var candidate in candidates)
            {
                var daysSinceCreated = (now.UtcDateTime.Date - candidate.CreatedAt.UtcDateTime.Date).Days;

                var alreadySentDays = await db.ScheduledNotifications.IgnoreQueryFilters()
                    .Where(n => n.RecipientLeadId == candidate.Id)
                    .Select(n => n.NotificationTemplateId)
                    .ToListAsync(cancellationToken);

                var sentDripDays = templatesByDay
                    .Where(kv => alreadySentDays.Contains(kv.Value))
                    .Select(kv => kv.Key)
                    .ToHashSet();

                var dueDay = LeadDripPolicy.GetDueDripDay(candidate.Stage, candidate.HasAnyActivity, daysSinceCreated, sentDripDays);
                if (dueDay is null || !templatesByDay.TryGetValue(dueDay.Value, out var templateId))
                {
                    continue;
                }

                db.ScheduledNotifications.Add(new ScheduledNotification
                {
                    TenantId = tenantId,
                    BranchId = candidate.BranchId,
                    NotificationTemplateId = templateId,
                    RecipientLeadId = candidate.Id,
                    ScheduledFor = now,
                    Status = ScheduledNotificationStatus.Pending,
                    RelatedEntityType = nameof(Lead),
                    RelatedEntityId = candidate.Id
                });
                scheduled++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Lead drip scheduled {Count} notification(s)", scheduled);
        return scheduled;
    }
}
