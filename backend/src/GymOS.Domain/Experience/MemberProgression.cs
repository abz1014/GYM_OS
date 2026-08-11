using GymOS.Domain.Common;

namespace GymOS.Domain.Experience;

/// <summary>
/// The blueprint's "MemberLevel": a member's current level and total XP. A projection over the
/// <see cref="XpTransaction"/> ledger (one row per member), kept current incrementally by the award
/// handler and fully rebuildable from the ledger — never the source of truth for what was earned.
/// TotalXp/Level are mutated only through <see cref="AddXp"/>/<see cref="SetTotalXp"/> so Level and
/// TotalXp can never drift apart.
/// </summary>
public class MemberProgression : AggregateRoot, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid MemberId { get; set; }

    /// <summary>What the ledger currently sums to. Can fall — undoing a mis-tapped workout removes the
    /// XP it granted, and that is correct accounting for a session that did not happen.</summary>
    public long TotalXp { get; private set; }

    /// <summary>
    /// The highest TotalXp ever reached. A ratchet: it only ever rises.
    ///
    /// This is what a member is shown and what <see cref="Level"/> derives from, and the two fields
    /// exist separately on purpose. TotalXp has to stay equal to the sum of the ledger or the
    /// projection is lying about its own source — there is a rebuild test that exists to enforce
    /// exactly that. But a member must never be demoted, and undo is the one path that could do it:
    /// correct a mis-tap noticed thirty seconds later and a rank earned over months could drop.
    ///
    /// Splitting them means the audit trail stays honest AND progress is permanent. What was earned
    /// was earned; the ledger separately records that a particular session was withdrawn.
    /// </summary>
    public long PeakXp { get; private set; }

    /// <summary>Derived from <see cref="PeakXp"/>, never from TotalXp, so it cannot go down.</summary>
    public int Level { get; private set; } = 1;

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Applies an incremental award (from the ledger), recomputes the level, and signals that
    /// this member advanced so downstream projections (achievements) re-evaluate against the committed
    /// result. Raised on the incremental path only — <see cref="SetTotalXp"/> (rebuild/seed) is silent.</summary>
    public void AddXp(int amount)
    {
        SetTotalXp(TotalXp + amount);
        AddDomainEvent(new MemberProgressionChangedEvent(MemberId));
    }

    /// <summary>Sets the absolute total (used by a full rebuild from the ledger, or seeding) and
    /// recomputes the level. Deliberately does not raise a change event.</summary>
    public void SetTotalXp(long totalXp)
    {
        TotalXp = totalXp < 0 ? 0 : totalXp;

        // The ratchet. A full rebuild replays a ledger that may have had rows removed by an undo, so
        // the peak cannot be recomputed from the ledger alone — it has to be carried, which is why it
        // is persisted rather than derived. Taking the max here means a rebuild restores the true
        // total without ever lowering a rank the member already holds.
        if (TotalXp > PeakXp)
        {
            PeakXp = TotalXp;
        }

        Level = XpPolicy.LevelForXp(PeakXp).Level;
    }

    /// <summary>
    /// Sets the total authoritatively, peak included — the ONE path allowed to lower a level.
    ///
    /// Reserved for a full projection rebuild, which exists to repair a projection that has drifted
    /// from its ledger. A drifted row can be wrong in either direction, and a rebuild that could only
    /// raise would make an inflated projection permanent: the corruption would outlive the tool built
    /// to fix it. So the ratchet deliberately does not apply here.
    ///
    /// The cost is real and worth stating: a rebuild run after a member has undone a workout resets
    /// the peak to the ledger's current sum, which can cost that member a level. That is acceptable
    /// because a rebuild is a rare administrative repair, whereas undo is a thing members do casually
    /// and often — the ratchet guards the common path and truth wins on the rare one.
    ///
    /// The way to remove the cost entirely is to stop deleting rows on undo and write a compensating
    /// negative transaction instead. The ledger would stay append-only, and the peak would become the
    /// running maximum of the cumulative sum — derivable, so a rebuild could restore it exactly. That
    /// is the better design and a larger change than this one.
    /// </summary>
    public void RebuildTo(long totalXp)
    {
        TotalXp = totalXp < 0 ? 0 : totalXp;
        PeakXp = TotalXp;
        Level = XpPolicy.LevelForXp(PeakXp).Level;
    }
}
