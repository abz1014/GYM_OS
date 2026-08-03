using Microsoft.AspNetCore.SignalR;

namespace GymOS.Infrastructure.RealTime;

/// <summary>Pushes live dashboard events (new check-in, new payment) so the Executive Dashboard updates without a refresh.</summary>
public class DashboardHub : Hub
{
    public async Task JoinBranchGroup(string branchId) => await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(branchId));

    public static string GroupName(string branchId) => $"branch:{branchId}";
}
