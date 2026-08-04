using GymOS.Domain.Workouts;

namespace GymOS.Application.Modules.Portal.Dtos;

public record MyNutritionSummaryDto(
    string? ActiveDietPlanName,
    decimal? TargetCalories,
    decimal? TargetProteinG,
    decimal? TargetCarbsG,
    decimal? TargetFatG,
    decimal ConsumedCalories,
    decimal ConsumedProteinG,
    decimal ConsumedCarbsG,
    decimal ConsumedFatG,
    int WaterMl);

public record MyExerciseSuggestionDto(
    Guid ExerciseId, string ExerciseName, string? MuscleGroup, OverloadSuggestion Suggestion,
    decimal? LastWeightKg, int? LastTotalReps, decimal? SuggestedNextWeightKg, DateTimeOffset LastLoggedAt);
