namespace GymOS.Application.Modules.Workouts.Dtos;

/// <param name="LoadType">Weighted, Bodyweight, Timed or Distance. Sent so the trainer's logging
/// dialog can ask for the measurements this movement actually has — the write path REJECTS a rep
/// count on a run, so a form that asks for one produces a 400 rather than a fiction.</param>
public record ExerciseDto(
    Guid Id, string Name, string? MuscleGroup, string? Equipment, string? Description, string? VideoUrl,
    string LoadType);

public record WorkoutTemplateExerciseDto(Guid Id, Guid ExerciseId, string ExerciseName, int SetsCount, int RepsCount, int OrderIndex);

public record WorkoutTemplateListItemDto(Guid Id, string Name, string? Description, int ExerciseCount);

public record WorkoutTemplateDetailDto(Guid Id, string Name, string? Description, IReadOnlyList<WorkoutTemplateExerciseDto> Exercises);

/// <param name="RepsCompleted">Null where the movement has none. A dash reads as "not measured";
/// a zero would read as "measured, and it was nothing".</param>
public record WorkoutLogEntryDto(
    Guid Id, Guid ExerciseId, string ExerciseName, int SetsCompleted, int? RepsCompleted, decimal? WeightKg,
    int? DurationSeconds, decimal? DistanceMeters);

/// <summary>
/// <paramref name="Character"/> is what the session was, derived from the muscle groups trained (see
/// SessionCharacterPolicy). It is always populated; a session logged against a trainer's template
/// still has a <paramref name="WorkoutTemplateName"/>, and that name — being the trainer's own words
/// — is the better one to show when it exists.
/// </summary>
public record WorkoutLogDto(
    Guid Id, Guid MemberId, Guid? WorkoutTemplateId, string? WorkoutTemplateName, string Character,
    DateTimeOffset LoggedAt, IReadOnlyList<WorkoutLogEntryDto> Entries);

public record WorkoutAssignmentListItemDto(
    Guid Id, Guid WorkoutTemplateId, string WorkoutTemplateName, string? TemplateDescription,
    DateOnly StartDate, DateOnly? EndDate, string? Notes, IReadOnlyList<WorkoutTemplateExerciseDto> Exercises);
