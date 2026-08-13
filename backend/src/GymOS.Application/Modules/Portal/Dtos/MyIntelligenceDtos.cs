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

/// <summary>
/// One movement's progressive-overload verdict.
/// </summary>
/// <param name="MuscleGroup">Canonical display name, resolved through MuscleGroupVocabulary.</param>
/// <param name="MuscleGroupKey">The canonical key. The Train screen cross-references these against
/// the recovery breakdown to decide which suggestions to hold back, so BOTH sides must resolve the
/// same way — matching a raw "Quads" against a recovered "Legs" silently offers a member a movement
/// their own screen has just marked as needing rest.</param>
public record MyExerciseSuggestionDto(
    Guid ExerciseId, string ExerciseName, string? MuscleGroup, string MuscleGroupKey,
    // Every canonical group the movement touches, primary and secondary alike — the Train screen's
    // headline reads this, because a deadlift classified by its primary alone is "upper body".
    IReadOnlyList<string> AllMuscleGroupKeys,
    OverloadSuggestion Suggestion,
    decimal? LastWeightKg, int? LastTotalReps, decimal? SuggestedNextWeightKg, DateTimeOffset LastLoggedAt);
