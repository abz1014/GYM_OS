namespace GymOS.Domain.Trainers;

/// <summary>
/// Who may say what to whom, and how a conversation reads once they have.
///
/// The rule that matters is the first one: correspondence follows an ACTIVE pairing and nothing
/// else. Without it, "message my trainer" becomes a way to reach any trainer, and "message my
/// client" a way to reach any member — the same shape as the cross-member exposure the portal
/// queries were built to prevent, and worse, because it carries free text between strangers.
///
/// A pairing that has ended keeps its history. The conversation happened; a member should be able to
/// read what their old coach told them. They just cannot add to it.
/// </summary>
public static class CoachMessagePolicy
{
    /// <summary>Long enough for real coaching, short enough not to be a document store.</summary>
    public const int MaxBodyLength = 2000;

    /// <summary>
    /// Whether a new message may be sent. Assignment dates are half-open the way the rest of the
    /// product reads them: a plan or pairing covers its start and end days inclusively.
    /// </summary>
    public static bool CanSend(DateOnly? assignmentStart, DateOnly? assignmentEnd, bool isActive, DateOnly today)
        => assignmentStart is DateOnly start
           && isActive
           && start <= today
           && (assignmentEnd is not DateOnly end || end >= today);

    /// <summary>
    /// Whether a body is worth sending. Whitespace is not a message, and the length bound exists so
    /// one side cannot make the other scroll a wall.
    /// </summary>
    public static bool IsSendable(string? body)
        => !string.IsNullOrWhiteSpace(body) && body.Trim().Length <= MaxBodyLength;

    /// <summary>The body as it should be stored — trailing whitespace is not content.</summary>
    public static string Normalise(string body) => body.Trim();

    /// <summary>
    /// How many messages the given side has not read. Only the other side's messages count: your own
    /// are not news to you, and counting them would put a badge on an empty conversation.
    /// </summary>
    public static int UnreadFor(
        CoachMessageAuthor viewer,
        IEnumerable<(CoachMessageAuthor Author, DateTimeOffset? ReadAt)> messages)
        => messages.Count(m => m.Author != viewer && m.ReadAt is null);
}
