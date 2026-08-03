using Microsoft.AspNetCore.SignalR;

namespace GymOS.Infrastructure.RealTime;

/// <summary>
/// Clients join a per-tenant group on connect; server-side code pushes via
/// IHubContext&lt;NotificationHub&gt; from command handlers/background jobs rather than the hub
/// itself invoking anything — the hub is just the connection/group membership endpoint.
/// </summary>
public class NotificationHub : Hub
{
    public async Task JoinTenantGroup(string tenantId) => await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(tenantId));

    public static string GroupName(string tenantId) => $"tenant:{tenantId}";
}
