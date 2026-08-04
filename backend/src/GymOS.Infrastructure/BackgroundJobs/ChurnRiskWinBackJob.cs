using GymOS.Application.Common.Interfaces;
using GymOS.Domain.Members;
using GymOS.Domain.Notifications;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GymOS.Infrastructure.BackgroundJobs;

/// <summary>
/// The automated win-back: finds paying members who have quietly stopped showing up and messages
/// them before they cancel. This is the retention automation an owner actually feels — a member who
/// drifts for a month usually never comes back, and nobody at the front desk notices in time.
///
/// Selection is delegated to ChurnRiskPolicy so the rule is unit-tested; this job only supplies the
/// facts (last check-in, last win-back already sent) and writes the scheduled notification.
/// </summary>
public class ChurnRiskWinBackJob(GymOsDbContext db, IDateTimeProvider dateTimeProvider, ILogger<ChurnRiskWinBackJob> logger)
{
    private const string TemplateCode = "churn-risk-winback";

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);
        var tenantIds = await db.Tenants.IgnoreQueryFilters().Select(t => t.Id).ToListAsync(cancellationToken);
        var scheduled = 0;

        foreach (var tenantId in tenantIds)
        {
            var template = await db.NotificationTemplates.IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Code == TemplateCode, cancellationToken);

            if (template is null)
            {
                continue;
            }

            // Only Active members can qualify (per the policy), so filter in SQL before pulling
            // per-member history — this keeps the scan proportional to paying members, not all rows.
            var candidates = await db.Members.IgnoreQueryFilters()
                .Where(m => m.TenantId == tenantId && m.Status == MemberStatus.Active)
                .Select(m => new
                {
                    m.Id,
                    m.BranchId,
                    LastCheckIn = db.AttendanceRecords.IgnoreQueryFilters()
                        .Where(a => a.MemberId == m.Id)
                        .Max(a => (DateTimeOffset?)a.CheckInAt),
                    LastWinBack = db.ScheduledNotifications.IgnoreQueryFilters()
                        .Where(n => n.RecipientMemberId == m.Id && n.NotificationTemplateId == template.Id)
                        .Max(n => (DateTimeOffset?)n.ScheduledFor)
                })
                .ToListAsync(cancellationToken);

            foreach (var candidate in candidates)
            {
                var lastCheckIn = candidate.LastCheckIn is null ? (DateOnly?)null : DateOnly.FromDateTime(candidate.LastCheckIn.Value.UtcDateTime);
                var lastWinBack = candidate.LastWinBack is null ? (DateOnly?)null : DateOnly.FromDateTime(candidate.LastWinBack.Value.UtcDateTime);

                if (!ChurnRiskPolicy.ShouldSendWinBack(MemberStatus.Active, lastCheckIn, lastWinBack, today))
                {
                    continue;
                }

                db.ScheduledNotifications.Add(new ScheduledNotification
                {
                    TenantId = tenantId,
                    BranchId = candidate.BranchId,
                    NotificationTemplateId = template.Id,
                    RecipientMemberId = candidate.Id,
                    ScheduledFor = dateTimeProvider.UtcNow,
                    Status = ScheduledNotificationStatus.Pending,
                    RelatedEntityType = nameof(Member),
                    RelatedEntityId = candidate.Id
                });
                scheduled++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Churn-risk win-back scheduled {Count} notification(s)", scheduled);
        return scheduled;
    }
}
