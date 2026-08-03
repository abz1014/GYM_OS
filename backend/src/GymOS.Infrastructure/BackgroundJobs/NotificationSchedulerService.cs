using GymOS.Application.Common.Interfaces;

namespace GymOS.Infrastructure.BackgroundJobs;

public class NotificationSchedulerService(MembershipExpiryCheckJob membershipExpiryCheckJob, NotificationDispatchJob notificationDispatchJob)
    : INotificationSchedulerService
{
    public Task<int> CheckMembershipExpiryAsync(CancellationToken cancellationToken) => membershipExpiryCheckJob.RunAsync(cancellationToken);

    public Task<int> DispatchDueNotificationsAsync(CancellationToken cancellationToken) => notificationDispatchJob.RunAsync(cancellationToken);
}
