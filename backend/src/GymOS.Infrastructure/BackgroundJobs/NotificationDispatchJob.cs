using GymOS.Application.Common.Interfaces;
using GymOS.Domain.Notifications;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GymOS.Infrastructure.BackgroundJobs;

/// <summary>Recurring job (registered every few minutes via Hangfire in Program.cs) that sends due ScheduledNotifications through the demo channel senders.</summary>
public class NotificationDispatchJob(
    GymOsDbContext db, IEmailSender emailSender, ISmsSender smsSender, IWhatsAppSender whatsAppSender,
    IDateTimeProvider dateTimeProvider, ILogger<NotificationDispatchJob> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var now = dateTimeProvider.UtcNow;

        var due = await db.ScheduledNotifications.IgnoreQueryFilters()
            .Include(n => n.NotificationTemplate)
            .Where(n => n.Status == ScheduledNotificationStatus.Pending && n.ScheduledFor <= now)
            .Take(200)
            .ToListAsync(cancellationToken);

        foreach (var notification in due)
        {
            var template = notification.NotificationTemplate;
            if (template is null)
            {
                notification.Status = ScheduledNotificationStatus.Failed;
                continue;
            }

            var recipientAddress = await ResolveRecipientAddressAsync(notification, template.Channel, cancellationToken);
            if (recipientAddress is null)
            {
                notification.Status = ScheduledNotificationStatus.Failed;
                continue;
            }

            switch (template.Channel)
            {
                case NotificationChannel.Sms:
                    await smsSender.SendAsync(recipientAddress, template.Subject, cancellationToken);
                    break;
                case NotificationChannel.WhatsApp:
                    await whatsAppSender.SendAsync(recipientAddress, template.Subject, cancellationToken);
                    break;
                case NotificationChannel.Email:
                case NotificationChannel.InApp:
                default:
                    await emailSender.SendAsync(recipientAddress, template.Subject, template.BodyTemplate, cancellationToken);
                    break;
            }

            notification.Status = ScheduledNotificationStatus.Sent;
        }

        var updated = await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Notification dispatch processed {Count} notification(s)", updated);
    }

    private async Task<string?> ResolveRecipientAddressAsync(ScheduledNotification notification, NotificationChannel channel, CancellationToken cancellationToken)
    {
        if (notification.RecipientMemberId is not null)
        {
            var member = await db.Members.IgnoreQueryFilters()
                .FirstOrDefaultAsync(m => m.Id == notification.RecipientMemberId, cancellationToken);

            return channel == NotificationChannel.Email ? member?.Email : member?.Phone;
        }

        if (notification.RecipientUserId is not null)
        {
            var user = await db.Users.IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == notification.RecipientUserId, cancellationToken);

            return channel == NotificationChannel.Email ? user?.Email : user?.Phone;
        }

        return null;
    }
}
