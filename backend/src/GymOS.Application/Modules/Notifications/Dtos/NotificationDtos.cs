namespace GymOS.Application.Modules.Notifications.Dtos;

public record NotificationTemplateDto(Guid Id, string Code, string Category, string Channel, string Subject, string BodyTemplate, bool IsActive);

public record ScheduledNotificationDto(
    Guid Id, string TemplateCode, string Category, string Channel,
    string? RecipientName, DateTimeOffset ScheduledFor, string Status);

public record NotificationLogDto(Guid Id, string Channel, string RecipientAddress, string Subject, string Body, DateTimeOffset SentAt, bool Success);

public record TriggerNotificationChecksResultDto(int ScheduledCount, int DispatchedCount);
