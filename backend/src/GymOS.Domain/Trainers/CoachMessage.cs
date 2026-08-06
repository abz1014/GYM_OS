using GymOS.Domain.Common;

namespace GymOS.Domain.Trainers;

/// <summary>Which side of the pairing wrote a message.</summary>
public enum CoachMessageAuthor
{
    Member,
    Trainer
}

/// <summary>
/// A message between a member and the trainer they are assigned to.
///
/// Deliberately not a general inbox. A chat surface asks a member to keep up with it — unread
/// badges, a reply owed — which is the opposite of what the rest of this portal has been built for.
/// What is worth carrying between a trainer and a member is a remark about specific training, so a
/// message can point at the session it is about (<see cref="WorkoutLogId"/>) and be read in place,
/// against the workout it refers to, rather than in a separate room with no context.
///
/// Tenant-scoped, so a global query filter keeps one gym's correspondence out of another's. The
/// narrower rule — that only an ACTIVE pairing may correspond at all — is CoachMessagePolicy's, and
/// is what stops a member reaching a trainer who is not theirs.
/// </summary>
public class CoachMessage : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid TrainerId { get; set; }

    public Guid MemberId { get; set; }

    public CoachMessageAuthor Author { get; set; }

    public string Body { get; set; } = string.Empty;

    public DateTimeOffset SentAt { get; set; }

    /// <summary>When the other side read it. Null while unread.</summary>
    public DateTimeOffset? ReadAt { get; set; }

    /// <summary>
    /// The session this message is about, when it is about one. Optional: a trainer commenting on a
    /// squat session sets it; "see you Thursday" does not.
    /// </summary>
    public Guid? WorkoutLogId { get; set; }
}
