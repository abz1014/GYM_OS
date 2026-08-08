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
    /// How many messages one side may send into one pairing per rolling hour.
    ///
    /// Set to be invisible to anybody using this as intended. A real exchange — a member describing
    /// a niggle, a coach working through it with them — might run ten messages in ten minutes, and
    /// this has to not interrupt that. What it stops is the unbounded case: a stuck client retrying,
    /// a script, or somebody using a coach's inbox to make a point at 3am. If a member hits this
    /// they are not having a conversation, and the honest response is to say so rather than to keep
    /// accepting writes.
    /// </summary>
    public const int MaxMessagesPerHour = 20;

    /// <summary>The window the limit is measured over. Rolling, not a fixed clock hour — a fixed
    /// hour lets somebody send the full allowance twice across a boundary a minute apart.</summary>
    public static readonly TimeSpan RateLimitWindow = TimeSpan.FromHours(1);

    /// <summary>
    /// How long a conversation is kept.
    ///
    /// This is the first retention rule in GymOS — nothing else in the product ages anything out —
    /// and it starts here because this is the only place two people write free text about someone's
    /// body and health to each other. Two years is chosen so no live coaching relationship ever
    /// loses context it might reach back for, while the correspondence does not simply accumulate
    /// forever by default. It is a floor to argue up or down from, not a number with a law behind it.
    /// </summary>
    public static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(365 * 2);

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
    /// Whether another message may be sent, given when this side's recent ones went.
    ///
    /// Takes the timestamps rather than a count so the window is applied here, in the one place the
    /// rule is written down, instead of by whichever caller happened to query. Counts only the
    /// sender's own messages: being replied to quickly is not a reason to be silenced.
    /// </summary>
    public static bool IsWithinRateLimit(IEnumerable<DateTimeOffset> ownRecentSentAt, DateTimeOffset now)
        => ownRecentSentAt.Count(sentAt => sentAt > now - RateLimitWindow) < MaxMessagesPerHour;

    /// <summary>
    /// Whether a message is old enough to remove. Compared against SentAt rather than read state —
    /// an unread message is not exempt from retention, and keeping it forever because nobody opened
    /// it would invert the rule.
    /// </summary>
    public static bool IsExpired(DateTimeOffset sentAt, DateTimeOffset now) => sentAt <= now - RetentionPeriod;

    /// <summary>
    /// How many messages the given side has not read. Only the other side's messages count: your own
    /// are not news to you, and counting them would put a badge on an empty conversation.
    /// </summary>
    public static int UnreadFor(
        CoachMessageAuthor viewer,
        IEnumerable<(CoachMessageAuthor Author, DateTimeOffset? ReadAt)> messages)
        => messages.Count(m => m.Author != viewer && m.ReadAt is null);
}
