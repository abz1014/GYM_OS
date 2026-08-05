using GymOS.Application.Common.Interfaces;

namespace GymOS.Application.Tests.TestSupport;

/// <summary>
/// Stands in for the SignalR dashboard push so command handlers that announce live activity (e.g.
/// CheckInCommand) can run in the Application test harness. Records what it was asked to broadcast
/// rather than discarding it, so a test can assert the signal was raised without needing a
/// transport.
/// </summary>
public class FakeDashboardNotifier : IDashboardNotifier
{
    public List<(Guid BranchId, string EventType)> Notifications { get; } = [];

    public Task NotifyBranchActivityAsync(Guid branchId, string eventType, CancellationToken cancellationToken = default)
    {
        Notifications.Add((branchId, eventType));
        return Task.CompletedTask;
    }
}
