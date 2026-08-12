using GymOS.Domain.Common;

namespace GymOS.Domain.Nutrition;

public class DietPlan : AggregateRoot, ITenantScoped
{
    /// <summary>
    /// Direct tenant scoping, so isolation is a property of the schema rather than of every query
    /// that happens to start from Member.
    ///
    /// This table was reachable only through a tenant-scoped Member, which made it safe in practice
    /// and unguarded in principle: one future query beginning here instead of at Member would cross
    /// tenants silently, with nothing failing. Same class of gap as the cross-branch IDOR, same fix —
    /// enforce it in the model so nobody has to remember.
    /// </summary>
    public Guid TenantId { get; set; }

    public Guid MemberId { get; set; }

    public string Name { get; set; } = string.Empty;

    public Guid? CreatedByUserId { get; set; }

    public decimal? TargetCalories { get; set; }

    // Optional macro targets alongside the calorie target — a plan can set calories only (the
    // common case) or go further and coach specific protein/carb/fat goals.
    public decimal? TargetProteinG { get; set; }

    public decimal? TargetCarbsG { get; set; }

    public decimal? TargetFatG { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    /// <summary>
    /// The standing instructions — how to eat on this plan, in the nutritionist's own words.
    ///
    /// The plan had four numbers, two dates and a name, and nowhere for a person to say anything at
    /// all. A calorie target is not advice, which is exactly why the member's nutrition screen read
    /// as useless: it could only show macros because macros were all there was.
    /// </summary>
    public string? Notes { get; set; }

    public ICollection<MealEntry> MealEntries { get; set; } = [];

    /// <summary>What changes week to week and month to month. Append-only — see DietPlanGuidance.</summary>
    public ICollection<DietPlanGuidance> Guidance { get; set; } = [];

    /// <summary>Signals the Member Experience Engine that this plan's member logged a meal on the
    /// given day. Called by AddMealEntryCommand after the entry is added; dispatched after save.</summary>
    public void RaiseMealLogged(DateOnly consumedDate) => AddDomainEvent(new MealLoggedEvent(MemberId, consumedDate));

    /// <summary>
    /// Signals that the member confirmed they stayed on this plan for a day.
    ///
    /// Takes the UTC day rather than the gym day, matching <see cref="RaiseMealLogged"/> exactly —
    /// the two events hash their date into the same idempotency key, so a difference of clocks
    /// between them is a difference of keys, and a member near midnight earns twice. See
    /// PlanAdherenceLoggedEvent.
    /// </summary>
    public void RaiseAdherenceLogged(DateOnly xpDayUtc) => AddDomainEvent(new PlanAdherenceLoggedEvent(MemberId, xpDayUtc));
}
