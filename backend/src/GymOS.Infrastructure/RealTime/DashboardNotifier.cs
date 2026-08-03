using GymOS.Application.Common.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace GymOS.Infrastructure.RealTime;

public class DashboardNotifier(IHubContext<DashboardHub> hubContext) : IDashboardNotifier
{
    public Task NotifyBranchActivityAsync(Guid branchId, string eventType, CancellationToken cancellationToken = default)
        => hubContext.Clients.Group(DashboardHub.GroupName(branchId.ToString())).SendAsync("activity", eventType, cancellationToken: cancellationToken);
}
