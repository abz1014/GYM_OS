namespace GymOS.Application.Modules.Portal.Dtos;

/// <summary>An exercise the member can pick when logging a workout.</summary>
public record LoggableExerciseDto(Guid Id, string Name, string? MuscleGroup, string? Equipment);

/// <summary>A food item the member can pick when logging a meal, with the per-serving macros the
/// picker shows so they can choose without a second round trip.</summary>
public record LoggableFoodDto(Guid Id, string Name, decimal CaloriesPerServing, decimal ProteinG, string ServingSizeDescription);

/// <summary>Everything the logging screen's pickers need. ActiveDietPlanName is null when the member
/// has no active plan — meal logging is disabled in that case rather than failing on submit.</summary>
public record MyLoggingOptionsDto(
    IReadOnlyList<LoggableExerciseDto> Exercises,
    IReadOnlyList<LoggableFoodDto> Foods,
    string? ActiveDietPlanName);

/// <summary>One dated body-measurement snapshot — the full row, unlike MyWeightPointDto which
/// carries only weight for the existing summary card.</summary>
public record MyMeasurementDto(
    Guid Id, DateOnly MeasuredOn, decimal? WeightKg, decimal? BodyFatPercentage,
    decimal? ChestCm, decimal? WaistCm, decimal? HipCm, decimal? ArmCm, decimal? ThighCm, string? Notes);

/// <summary>One day of training volume. Rest days are present with zeroes so the chart shows real
/// training cadence instead of compressing the gaps between sessions.</summary>
public record MyDailyVolumeDto(DateOnly Date, decimal VolumeKg, int TotalReps);
