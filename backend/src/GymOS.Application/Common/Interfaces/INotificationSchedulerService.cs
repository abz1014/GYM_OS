namespace GymOS.Application.Common.Interfaces;

/// <summary>
/// Wraps the Hangfire recurring jobs (membership expiry check, notification dispatch) so the
/// Notification Center's "Run checks now" demo action can trigger them synchronously without
/// Application depending on Infrastructure's job classes directly.
/// </summary>
public interface INotificationSchedulerService
{
    Task<int> CheckMembershipExpiryAsync(CancellationToken cancellationToken);

    Task<int> DispatchDueNotificationsAsync(CancellationToken cancellationToken);
}
