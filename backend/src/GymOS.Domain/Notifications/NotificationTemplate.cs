using GymOS.Domain.Common;

namespace GymOS.Domain.Notifications;

public class NotificationTemplate : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public string Code { get; set; } = string.Empty;

    public NotificationCategory Category { get; set; }

    public NotificationChannel Channel { get; set; }

    public string Subject { get; set; } = string.Empty;

    public string BodyTemplate { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
