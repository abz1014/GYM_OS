using GymOS.Domain.Common;

namespace GymOS.Domain.Nutrition;

/// <summary>
/// One day a member says they stayed on their plan. One tap, one row, at most one per day.
///
/// WHY THIS EXISTS. The member's nutrition screen became a prescription to read rather than a diary
/// to fill in, which is what a nutrition plan actually is. But two things were resting on meal
/// logging, and removing the prompt without replacing them would have broken both silently:
///
/// 1. NutritionAdherence XP — 15 a day — had exactly one award site, on MealLoggedEvent. Members
///    would simply have stopped earning it, while the rank screen carried on advertising the tip
///    that says "Follow your nutrition plan · +15".
/// 2. CoachingPolicy.NutritionAdherencePercent counts days a meal was logged against days the plan
///    was active, and it returns 0% — not null — for a member who HAS a plan and logs nothing. Every
///    trainer dashboard would have asserted that every member was ignoring their diet.
///
/// A tick is not a food diary. It is the smallest honest instrument for the thing the metric is
/// actually named after: adherence is a self-report, and it always was — counting meal rows was only
/// ever a proxy for it. Measuring the thing directly is both less work for the member and a better
/// number for the nutritionist.
///
/// Meal logging is deliberately NOT removed. It still lives on /log-activity for members who want the
/// detail, still awards the same XP through the same day key, and still feeds the Reports nutrition
/// tab. This is a second way to say the same thing, not a replacement.
/// </summary>
public class PlanAdherenceLog : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid MemberId { get; set; }

    /// <summary>
    /// The plan this was against. Kept even though the member id would do: a tick means "I followed
    /// THAT", and a member who is re-prescribed mid-week has a different that.
    /// </summary>
    public Guid DietPlanId { get; set; }

    /// <summary>
    /// The gym-clock day. A date, not a timestamp — "did you stay on plan today" has no time of day,
    /// and the uniqueness rule that makes this idempotent is per calendar day.
    /// </summary>
    public DateOnly OnDate { get; set; }

    /// <summary>Optional, and the reason the tick is worth more than a boolean to a nutritionist.</summary>
    public string? Note { get; set; }

    public DateTimeOffset LoggedAt { get; set; }
}
