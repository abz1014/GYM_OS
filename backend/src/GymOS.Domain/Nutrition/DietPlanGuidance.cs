using GymOS.Domain.Common;

namespace GymOS.Domain.Nutrition;

/// <summary>How often a piece of guidance is meant to be replaced.</summary>
public enum GuidanceCadence
{
    /// <summary>This week's adjustment. Superseded by the next weekly note.</summary>
    Weekly,
    /// <summary>The block-level instruction — the shape of the next month.</summary>
    Monthly
}

/// <summary>
/// What the nutritionist wants this member to do, this week or this month.
///
/// The gap this fills: DietPlan carried four numeric targets, a date range and a name, and nowhere at
/// all for a human being to say anything. So the member's nutrition screen could only ever show
/// macros — and the owner's verdict on it ("useless") was fair, because a calorie target is not
/// coaching. There was also no UpdateDietPlanCommand, which meant "the plan changes weekly" could
/// only be expressed by inserting a whole new plan row and letting the old one lapse.
///
/// Guidance is append-only on purpose. A member should be able to look back at what they were told
/// three weeks ago and see that it changed — that history IS the sense of a plan progressing, and
/// overwriting a single note would erase it. The current guidance is simply the newest one whose
/// EffectiveFrom has arrived, per cadence, so a nutritionist can queue next week's in advance.
/// </summary>
public class DietPlanGuidance : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid DietPlanId { get; set; }

    public DietPlan? DietPlan { get; set; }

    /// <summary>Weekly or monthly. Both can be live at once — they answer different questions.</summary>
    public GuidanceCadence Cadence { get; set; }

    /// <summary>
    /// The day this becomes the current guidance. A date rather than a timestamp because a member
    /// reads "this week", not "since 09:14" — and because it lets a nutritionist write Monday's note
    /// on Friday without it appearing early.
    /// </summary>
    public DateOnly EffectiveFrom { get; set; }

    /// <summary>The headline — "Carbs down on rest days". Short enough to read at a glance.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The detail. Optional: a good weekly note is often just its title.</summary>
    public string? Body { get; set; }

    /// <summary>The nutritionist who wrote it, so the member knows whose advice this is.</summary>
    public Guid? CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
