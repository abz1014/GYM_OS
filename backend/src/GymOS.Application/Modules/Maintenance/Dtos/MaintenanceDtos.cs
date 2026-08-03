using GymOS.Domain.Maintenance;

namespace GymOS.Application.Modules.Maintenance.Dtos;

public record WorkOrderListItemDto(
    Guid Id, string AssetName, string AssetTag, WorkOrderType Type, WorkOrderPriority Priority, WorkOrderStatus Status,
    string Title, DateOnly? ScheduledDate, bool IsOverdue);

public record DowntimeLogDto(Guid Id, DateTimeOffset StartedAt, DateTimeOffset? EndedAt, string? Reason);

public record WorkOrderDetailDto(
    Guid Id, Guid AssetId, string AssetName, string AssetTag, WorkOrderType Type, WorkOrderPriority Priority,
    WorkOrderStatus Status, string Title, string? Description, Guid? AssignedToUserId, DateOnly? ScheduledDate,
    DateOnly? CompletedDate, decimal? Cost, IReadOnlyList<DowntimeLogDto> DowntimeLogs,
    Guid? MaintenanceScheduleId, string? VerificationNotes, DateTimeOffset? VerifiedAt);

public record MaintenanceScheduleDto(Guid Id, Guid AssetId, string AssetName, string RecurrenceRule, DateOnly NextDueDate, bool IsActive);
