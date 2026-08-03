namespace GymOS.Application.Modules.Workouts.Dtos;

public record ExerciseDto(Guid Id, string Name, string? MuscleGroup, string? Equipment, string? Description, string? VideoUrl);

public record WorkoutTemplateExerciseDto(Guid Id, Guid ExerciseId, string ExerciseName, int SetsCount, int RepsCount, int OrderIndex);

public record WorkoutTemplateListItemDto(Guid Id, string Name, string? Description, int ExerciseCount);

public record WorkoutTemplateDetailDto(Guid Id, string Name, string? Description, IReadOnlyList<WorkoutTemplateExerciseDto> Exercises);

public record WorkoutLogEntryDto(Guid Id, Guid ExerciseId, string ExerciseName, int SetsCompleted, int RepsCompleted, decimal? WeightKg);

public record WorkoutLogDto(Guid Id, Guid MemberId, Guid? WorkoutTemplateId, string? WorkoutTemplateName, DateTimeOffset LoggedAt, IReadOnlyList<WorkoutLogEntryDto> Entries);
