using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Notifications.Dtos;
using MediatR;

namespace GymOS.Application.Modules.Notifications.Commands;

/// <summary>Demo "Run checks now" action — synchronously runs the same jobs Hangfire schedules on a cron, so the Dev Mailbox fills in immediately instead of waiting for the next tick.</summary>
public record TriggerNotificationChecksCommand : ICommand<TriggerNotificationChecksResultDto>;

public class TriggerNotificationChecksCommandHandler(INotificationSchedulerService scheduler)
    : IRequestHandler<TriggerNotificationChecksCommand, TriggerNotificationChecksResultDto>
{
    public async Task<TriggerNotificationChecksResultDto> Handle(TriggerNotificationChecksCommand request, CancellationToken cancellationToken)
    {
        var scheduledCount = await scheduler.CheckMembershipExpiryAsync(cancellationToken)
            + await scheduler.CheckBirthdaysAsync(cancellationToken)
            + await scheduler.CheckMaintenanceDueAsync(cancellationToken)
            + await scheduler.CheckLowStockAsync(cancellationToken)
            + await scheduler.CheckFollowUpRemindersAsync(cancellationToken);

        var dispatchedCount = await scheduler.DispatchDueNotificationsAsync(cancellationToken);

        return new TriggerNotificationChecksResultDto(scheduledCount, dispatchedCount);
    }
}
