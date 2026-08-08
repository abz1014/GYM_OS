namespace GymOS.Application.Common.Interfaces;

/// <summary>
/// Tells the other side of a coaching conversation that it moved, so their screen updates without a
/// refresh. Implemented over SignalR in Infrastructure and kept out of Application for the same
/// reason IDashboardNotifier is — a command handler should not know what the transport is.
///
/// It carries no message text, and that is a deliberate limit rather than an oversight. The client
/// reacts by refetching through the ordinary authorised endpoint, which already knows who may read
/// what. Pushing the body instead would put message content on a channel whose only access control
/// is group membership, and make every future change to that membership a content-leak risk. The
/// push is a nudge; the API remains the only thing that hands over words.
/// </summary>
public interface ICoachingNotifier
{
    /// <summary>Nudges one user — the recipient of a message, never the sender.</summary>
    Task NotifyConversationChangedAsync(Guid recipientUserId, CancellationToken cancellationToken = default);
}
