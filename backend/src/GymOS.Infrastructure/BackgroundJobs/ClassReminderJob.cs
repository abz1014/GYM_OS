using GymOS.Application.Common.Interfaces;
using GymOS.Domain.Classes;
using GymOS.Domain.Notifications;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GymOS.Infrastructure.BackgroundJobs;

/// <summary>
/// Reminds members about a class they booked, shortly before it starts — the nudge that turns a
/// booking into an attendance and keeps no-show rates (and therefore wasted capacity) down.
///
/// Runs on the same 5-minute cadence as notification dispatch rather than daily, because a reminder
/// is only useful inside a narrow window before the session. Dedup is by existing scheduled
/// notification per booking, so repeated runs inside the window don't spam.
/// </summary>
public class ClassReminderJob(GymOsDbContext db, IDateTimeProvider dateTimeProvider, ILogger<ClassReminderJob> logger)
{
    private const string TemplateCode = "class-reminder";

    /// <summary>How far ahead of a session to remind. Long enough to still travel, short enough to be relevant.</summary>
    private static readonly TimeSpan ReminderLeadTime = TimeSpan.FromHours(3);

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var now = dateTimeProvider.UtcNow;
        var windowEnd = now.Add(ReminderLeadTime);

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

            var upcoming = await db.ClassBookings.IgnoreQueryFilters()
                .Include(b => b.ClassSession)
                .Where(b => b.TenantId == tenantId
                            && b.Status == ClassBookingStatus.Booked
                            && b.ClassSession!.Status == ClassSessionStatus.Scheduled
                            && b.ClassSession.StartsAt > now
                            && b.ClassSession.StartsAt <= windowEnd
                            // Not already reminded for this specific booking.
                            && !db.ScheduledNotifications.IgnoreQueryFilters().Any(
                                n => n.NotificationTemplateId == template.Id
                                     && n.RelatedEntityType == "ClassBooking"
                                     && n.RelatedEntityId == b.Id))
                .ToListAsync(cancellationToken);

            foreach (var booking in upcoming)
            {
                db.ScheduledNotifications.Add(new ScheduledNotification
                {
                    TenantId = tenantId,
                    BranchId = booking.BranchId,
                    NotificationTemplateId = template.Id,
                    RecipientMemberId = booking.MemberId,
                    ScheduledFor = now,
                    Status = ScheduledNotificationStatus.Pending,
                    RelatedEntityType = nameof(ClassBooking),
                    RelatedEntityId = booking.Id
                });
                scheduled++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Class reminder scheduled {Count} notification(s)", scheduled);
        return scheduled;
    }
}
