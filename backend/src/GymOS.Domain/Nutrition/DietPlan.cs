using GymOS.Domain.Common;

namespace GymOS.Domain.Nutrition;

public class DietPlan : BaseEntity
{
    public Guid MemberId { get; set; }

    public string Name { get; set; } = string.Empty;

    public Guid? CreatedByUserId { get; set; }

    public decimal? TargetCalories { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public ICollection<MealEntry> MealEntries { get; set; } = [];
}
