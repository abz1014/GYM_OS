using GymOS.Application.Common.Interfaces;

namespace GymOS.Infrastructure.BackgroundJobs;

public class NotificationSchedulerService(
    MembershipExpiryCheckJob membershipExpiryCheckJob,
    BirthdayCheckJob birthdayCheckJob,
    MaintenanceDueCheckJob maintenanceDueCheckJob,
    LowStockCheckJob lowStockCheckJob,
    FollowUpReminderCheckJob followUpReminderCheckJob,
    NotificationDispatchJob notificationDispatchJob)
    : INotificationSchedulerService
{
    public Task<int> CheckMembershipExpiryAsync(CancellationToken cancellationToken) => membershipExpiryCheckJob.RunAsync(cancellationToken);

    public Task<int> CheckBirthdaysAsync(CancellationToken cancellationToken) => birthdayCheckJob.RunAsync(cancellationToken);

    public Task<int> CheckMaintenanceDueAsync(CancellationToken cancellationToken) => maintenanceDueCheckJob.RunAsync(cancellationToken);

    public Task<int> CheckLowStockAsync(CancellationToken cancellationToken) => lowStockCheckJob.RunAsync(cancellationToken);

    public Task<int> CheckFollowUpRemindersAsync(CancellationToken cancellationToken) => followUpReminderCheckJob.RunAsync(cancellationToken);

    public Task<int> DispatchDueNotificationsAsync(CancellationToken cancellationToken) => notificationDispatchJob.RunAsync(cancellationToken);
}
