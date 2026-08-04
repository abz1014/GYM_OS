using GymOS.Domain.Common;

namespace GymOS.Domain.Experience;

/// <summary>
/// A record that a member unlocked an achievement, once. The definition itself (name, description,
/// tier) lives in the code <see cref="AchievementCatalog"/> — a fixed, deterministic rule set, like
/// the other domain policies — so this row only needs the <see cref="Code"/> that keys back into it.
/// Append-only and unlock-once (unique per member+code); not <see cref="IAuditable"/> since it's
/// written by an event handler and carries its own <see cref="UnlockedAt"/>.
/// </summary>
public class MemberAchievement : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid MemberId { get; set; }

    public string Code { get; set; } = string.Empty;

    public DateTimeOffset UnlockedAt { get; set; }
}
