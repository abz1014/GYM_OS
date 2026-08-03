using GymOS.Domain.Common;

namespace GymOS.Domain.Nutrition;

public class MealEntry : BaseEntity
{
    public Guid DietPlanId { get; set; }

    public DietPlan? DietPlan { get; set; }

    public Guid FoodItemId { get; set; }

    public MealType MealType { get; set; }

    public decimal Quantity { get; set; }

    public DateTimeOffset? ConsumedAt { get; set; }
}
