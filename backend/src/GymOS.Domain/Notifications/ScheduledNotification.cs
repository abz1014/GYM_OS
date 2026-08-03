using GymOS.Domain.Common;

namespace GymOS.Domain.Notifications;

public class ScheduledNotification : BaseEntity, IBranchScoped
{
    public Guid TenantId { get; set; }

    public Guid BranchId { get; set; }

    public Guid NotificationTemplateId { get; set; }

    public NotificationTemplate? NotificationTemplate { get; set; }

    public Guid? RecipientUserId { get; set; }

    public Guid? RecipientMemberId { get; set; }

    public DateTimeOffset ScheduledFor { get; set; }

    public ScheduledNotificationStatus Status { get; set; } = ScheduledNotificationStatus.Pending;

    public string? RelatedEntityType { get; set; }

    public Guid? RelatedEntityId { get; set; }
}
