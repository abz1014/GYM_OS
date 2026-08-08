using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace GymOS.Infrastructure.RealTime;

/// <summary>
/// Live delivery for coach↔member messages.
///
/// **This hub deliberately does not work like its two neighbours.** DashboardHub and
/// NotificationHub expose a Join method taking an id from the caller, which is acceptable for a
/// branch activity ping — the payload is a bare "something happened" and the worst case is an idle
/// refresh. It would not be acceptable here: a client naming its own group is a client choosing
/// whose conversation to listen to. So there is no join method at all. The server reads the caller's
/// identity from their token on connect and puts them in their own group, and that is the only group
/// they will ever be in.
///
/// The push carries no message text either — see ICoachingNotifier. Even if the group logic were
/// wrong one day, what leaks is the fact that a conversation moved, not a word of what was said.
/// </summary>
[Authorize]
public class CoachingHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? Context.User?.FindFirstValue("sub");

        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(userId));
        }

        await base.OnConnectedAsync();
    }

    /// <summary>The only group anyone is ever placed in: their own, decided here rather than asked for.</summary>
    public static string GroupName(string userId) => $"user:{userId}";
}
