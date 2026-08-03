namespace GymOS.Application.Common.Interfaces;

/// <summary>Abstraction over pushing a live dashboard refresh signal — implemented via SignalR in Infrastructure, kept out of Application so command handlers don't depend on the realtime transport.</summary>
public interface IDashboardNotifier
{
    Task NotifyBranchActivityAsync(Guid branchId, string eventType, CancellationToken cancellationToken = default);
}
