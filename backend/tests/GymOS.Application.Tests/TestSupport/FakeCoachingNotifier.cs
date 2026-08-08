using GymOS.Application.Common.Interfaces;

namespace GymOS.Application.Tests.TestSupport;

/// <summary>
/// Records who would have been nudged instead of opening a socket. Recording rather than discarding
/// because "the right person was told" is a real assertion — a push sent to the sender, or to
/// nobody, is a bug the send path would otherwise hide.
/// </summary>
public class FakeCoachingNotifier : ICoachingNotifier
{
    public List<Guid> Notified { get; } = [];

    public Task NotifyConversationChangedAsync(Guid recipientUserId, CancellationToken cancellationToken = default)
    {
        Notified.Add(recipientUserId);
        return Task.CompletedTask;
    }
}
