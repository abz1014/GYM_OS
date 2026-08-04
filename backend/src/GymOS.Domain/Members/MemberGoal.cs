using GymOS.Domain.Common;

namespace GymOS.Domain.Members;

/// <summary>
/// A member's own stated objective ("Bench 100kg", "Down to 80kg by December"). Deliberately a
/// free-text title + optional target date rather than a typed metric system — members phrase goals
/// in their own words, and the motivational value is in seeing the goal and ticking it off, not in
/// machine-readable targets. Tenant-scoped; ownership is by MemberId and every portal path resolves
/// that server-side (same rule as the rest of /api/me).
/// </summary>
public class MemberGoal : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid MemberId { get; set; }

    public Member? Member { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateOnly? TargetDate { get; set; }

    public bool IsAchieved { get; set; }

    public DateTimeOffset? AchievedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
