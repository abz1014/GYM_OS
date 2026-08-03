using GymOS.Domain.Nutrition;

namespace GymOS.Application.Modules.Nutrition.Dtos;

public record FoodItemDto(Guid Id, string Name, decimal CaloriesPerServing, decimal ProteinG, decimal CarbsG, decimal FatG, string ServingSizeDescription);

public record MealEntryDto(Guid Id, Guid FoodItemId, string FoodItemName, MealType MealType, decimal Quantity, DateTimeOffset? ConsumedAt);

public record DietPlanListItemDto(Guid Id, string Name, decimal? TargetCalories, DateOnly StartDate, DateOnly? EndDate);

public record DietPlanDetailDto(
    Guid Id, Guid MemberId, string Name, decimal? TargetCalories, DateOnly StartDate, DateOnly? EndDate,
    IReadOnlyList<MealEntryDto> MealEntries);

public record WaterLogDto(Guid Id, int AmountMl, DateTimeOffset LoggedAt);
