using GymOS.Domain.Common;

namespace GymOS.Domain.Members;

/// <summary>
/// A member's own training-cadence setting: how many sessions a week they are aiming for.
///
/// This is member-authored input, and where it lives matters. It is deliberately NOT on
/// <see cref="GymOS.Domain.Experience.MemberProgression"/> — that is a projection rebuilt from the XP
/// ledger, so RebuildExperienceProjectionsCommand would silently reset every member's chosen goal.
/// It is deliberately not a <see cref="MemberGoal"/> either: those are free-text aspirations
/// ("bench 100kg") that get ticked off once, whereas this is a standing weekly target the home
/// screen measures against every week.
///
/// One row per member, written lazily on first change. An absent row means "never customised" and
/// reads as <see cref="WeeklyGoalPolicy.DefaultWeeklySessionGoal"/>, so existing members need no
/// backfill and the default stays in one place rather than being copied into every member row.
/// </summary>
public class MemberTrainingPreference : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid MemberId { get; set; }

    public Member? Member { get; set; }

    public int WeeklySessionGoal { get; set; } = WeeklyGoalPolicy.DefaultWeeklySessionGoal;

    public DateTimeOffset UpdatedAt { get; set; }
}
