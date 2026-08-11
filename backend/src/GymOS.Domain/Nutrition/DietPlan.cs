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

    public ICollection<MealEntry> MealEntries { get; set; } = [];

    /// <summary>Signals the Member Experience Engine that this plan's member logged a meal on the
    /// given day. Called by AddMealEntryCommand after the entry is added; dispatched after save.</summary>
    public void RaiseMealLogged(DateOnly consumedDate) => AddDomainEvent(new MealLoggedEvent(MemberId, consumedDate));
}
