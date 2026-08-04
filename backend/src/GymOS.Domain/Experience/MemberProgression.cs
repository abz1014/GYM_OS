using GymOS.Domain.Common;

namespace GymOS.Domain.Experience;

/// <summary>
/// The blueprint's "MemberLevel": a member's current level and total XP. A projection over the
/// <see cref="XpTransaction"/> ledger (one row per member), kept current incrementally by the award
/// handler and fully rebuildable from the ledger — never the source of truth for what was earned.
/// TotalXp/Level are mutated only through <see cref="AddXp"/>/<see cref="SetTotalXp"/> so Level and
/// TotalXp can never drift apart.
/// </summary>
public class MemberProgression : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid MemberId { get; set; }

    public long TotalXp { get; private set; }

    public int Level { get; private set; } = 1;

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Applies an incremental award (from the ledger) and recomputes the level.</summary>
    public void AddXp(int amount) => SetTotalXp(TotalXp + amount);

    /// <summary>Sets the absolute total (used by a full rebuild from the ledger) and recomputes the level.</summary>
    public void SetTotalXp(long totalXp)
    {
        TotalXp = totalXp < 0 ? 0 : totalXp;
        Level = XpPolicy.LevelForXp(TotalXp).Level;
    }
}
