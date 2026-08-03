using GymOS.Application.Common.Interfaces;
using GymOS.Domain.Members;
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
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
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

            var resolved = await ResolveRecipientAsync(notification, template.Channel, cancellationToken);
            if (resolved is null)
            {
                notification.Status = ScheduledNotificationStatus.Failed;
                continue;
            }

            var (recipientAddress, placeholders) = resolved.Value;
            var subject = ApplyPlaceholders(template.Subject, placeholders);
            var body = ApplyPlaceholders(template.BodyTemplate, placeholders);

            switch (template.Channel)
            {
                case NotificationChannel.Sms:
                    await smsSender.SendAsync(recipientAddress, subject, cancellationToken);
                    break;
                case NotificationChannel.WhatsApp:
                    await whatsAppSender.SendAsync(recipientAddress, subject, cancellationToken);
                    break;
                case NotificationChannel.Email:
                case NotificationChannel.InApp:
                default:
                    await emailSender.SendAsync(recipientAddress, subject, body, cancellationToken);
                    break;
            }

            notification.Status = ScheduledNotificationStatus.Sent;
        }

        var updated = await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Notification dispatch processed {Count} notification(s)", updated);
        return updated;
    }

    private async Task<(string Address, Dictionary<string, string> Placeholders)?> ResolveRecipientAsync(
        ScheduledNotification notification, NotificationChannel channel, CancellationToken cancellationToken)
    {
        var placeholders = new Dictionary<string, string>();
        string? address = null;

        if (notification.RecipientMemberId is not null)
        {
            var member = await db.Members.IgnoreQueryFilters()
                .FirstOrDefaultAsync(m => m.Id == notification.RecipientMemberId, cancellationToken);

            if (member is null)
            {
                return null;
            }

            address = channel == NotificationChannel.Email ? member.Email : member.Phone;
            placeholders["FirstName"] = member.FirstName;
            placeholders["LastName"] = member.LastName;
        }
        else if (notification.RecipientUserId is not null)
        {
            var user = await db.Users.IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == notification.RecipientUserId, cancellationToken);

            if (user is null)
            {
                return null;
            }

            address = channel == NotificationChannel.Email ? user.Email : user.Phone;
            placeholders["FirstName"] = user.FirstName;
            placeholders["LastName"] = user.LastName;
        }

        if (string.IsNullOrEmpty(address))
        {
            return null;
        }

        if (notification.RelatedEntityType == nameof(MemberMembership) && notification.RelatedEntityId is not null)
        {
            var membership = await db.MemberMemberships.IgnoreQueryFilters()
                .FirstOrDefaultAsync(mm => mm.Id == notification.RelatedEntityId, cancellationToken);

            if (membership is not null)
            {
                placeholders["ExpiryDate"] = membership.EndDate.ToString("MMM d, yyyy");
            }
        }

        return (address, placeholders);
    }

    private static string ApplyPlaceholders(string template, Dictionary<string, string> placeholders)
    {
        foreach (var (key, value) in placeholders)
        {
            template = template.Replace("{{" + key + "}}", value);
        }

        return template;
    }
}
