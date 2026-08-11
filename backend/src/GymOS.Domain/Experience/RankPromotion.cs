using GymOS.Domain.Common;

namespace GymOS.Domain.Experience;

/// <summary>
/// The moment a member reached a rung, kept forever.
///
/// A row rather than a flag, because the flaw in the celebration this joins was that it existed only
/// in a response body: the workout logger returned LeveledUp, the screen showed something, and it was
/// gone — unrepeatable, unreferenced, invisible to anyone who happened to close the app first. A rank
/// is the most durable thing a member earns, so the moment they earned it is worth a record.
///
/// Append-only and unique per (member, tier). That uniqueness is not just hygiene — it is what makes
/// the whole thing idempotent without any "already celebrated?" bookkeeping. PeakXp ratchets, so a
/// tier is reached exactly once in a member's life, and the handler can therefore run on every XP
/// award and simply insert what is missing.
/// </summary>
public class RankPromotion : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid MemberId { get; set; }

    public RankTier Tier { get; set; }

    public DateTimeOffset AchievedAt { get; set; }

    /// <summary>
    /// False until the member has actually been shown it.
    ///
    /// Separate from AchievedAt because the two answer different questions, and conflating them is how
    /// a celebration gets missed: XP can be earned by a Hangfire job or a front-desk check-in while
    /// the member's phone is in a locker. Without this, "did they see it" collapses into "did it
    /// happen recently", and a promotion earned on Friday evening is silently spent by Monday.
    /// </summary>
    public bool Seen { get; set; }
}
