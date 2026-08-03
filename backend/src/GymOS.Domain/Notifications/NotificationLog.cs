using GymOS.Domain.Common;

namespace GymOS.Domain.Notifications;

public class NotificationLog : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid? ScheduledNotificationId { get; set; }

    public NotificationChannel Channel { get; set; }

    public string RecipientAddress { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public DateTimeOffset SentAt { get; set; }

    public bool Success { get; set; } = true;

    public string? ErrorMessage { get; set; }
}
