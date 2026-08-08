using GymOS.Application.Common.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace GymOS.Infrastructure.RealTime;

/// <summary>SignalR implementation of <see cref="ICoachingNotifier"/>. See CoachingHub for why the
/// group is the recipient's own and why nothing but a signal travels over it.</summary>
public class CoachingNotifier(IHubContext<CoachingHub> hubContext) : ICoachingNotifier
{
    public Task NotifyConversationChangedAsync(Guid recipientUserId, CancellationToken cancellationToken = default)
        => hubContext.Clients
            .Group(CoachingHub.GroupName(recipientUserId.ToString()))
            .SendAsync("conversationChanged", cancellationToken);
}
