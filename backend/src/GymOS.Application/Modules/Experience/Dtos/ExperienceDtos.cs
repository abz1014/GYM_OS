namespace GymOS.Application.Modules.Experience.Dtos;

/// <summary>The member's progression snapshot: current level, total XP, progress within the current
/// level, and their most recent ledger entries (for a "recent activity" strip).</summary>
public record MyExperienceDto(
    int Level,
    long TotalXp,
    long XpIntoLevel,
    long XpForNextLevel,
    IReadOnlyList<MyXpEntryDto> Recent);

public record MyXpEntryDto(int Amount, string Reason, DateTimeOffset OccurredAt);

/// <summary>A member's current best on one exercise/metric (the max of an append-only PR ledger).</summary>
public record MyPersonalRecordDto(Guid ExerciseId, string ExerciseName, string Type, decimal Value, DateTimeOffset AchievedAt);

/// <summary>Mastery broken down three ways — per exercise, and aggregated by muscle group and by
/// machine (equipment) — each carrying a bounded 0–100 mastery percent.</summary>
public record MyMasteryDto(
    IReadOnlyList<ExerciseMasteryDto> Exercises,
    IReadOnlyList<GroupMasteryDto> MuscleGroups,
    IReadOnlyList<GroupMasteryDto> Machines);

public record ExerciseMasteryDto(
    Guid ExerciseId,
    string ExerciseName,
    string? MuscleGroup,
    string? Equipment,
    int Sessions,
    decimal BestWeightKg,
    decimal BestEstimatedOneRepMax,
    decimal TotalVolume,
    int MasteryPercent,
    DateTimeOffset LastTrainedAt);

public record GroupMasteryDto(string Name, int Sessions, decimal TotalVolume, int MasteryPercent);

/// <summary>One achievement on the member's shelf — the catalog definition plus whether they've
/// unlocked it (and when). Locked ones are returned too, so the shelf can show what's next.</summary>
public record MyAchievementDto(
    string Code,
    string Name,
    string Description,
    string Tier,
    string Category,
    string Icon,
    bool Unlocked,
    DateTimeOffset? UnlockedAt);

/// <summary>A member's habit streaks — consecutive weeks with at least one attendance, workout, and
/// logged meal respectively (all via the same weekly StreakCalculator).</summary>
public record MyStreaksDto(int AttendanceWeeks, int WorkoutWeeks, int NutritionWeeks);

/// <summary>One coaching nudge — always carries a plain-language Explanation ("always explain").</summary>
public record MyRecommendationDto(string Type, string Title, string Explanation, Guid? ExerciseId);

/// <summary>A member's recovery snapshot — an overall status + reason from recent training load, the
/// raw signals behind it, and a per-muscle-group breakdown. All derived from logged workouts and rest
/// days (no wearable data), so it is a pure read-model recomputed every request.</summary>
public record MyRecoveryDto(
    string Status,
    string Reason,
    int SessionsLast7Days,
    int RestDaysLast7Days,
    int? DaysSinceLastWorkout,
    IReadOnlyList<MuscleRecoveryDto> MuscleGroups);

public record MuscleRecoveryDto(
    string MuscleGroup,
    string Status,
    string Reason,
    int TimesLast7Days,
    int? DaysSinceLastTrained);

/// <summary>One entry in a member's story — sessions, measurements, photos, achieved goals and
/// unlocked achievements, ordered by when they happened. Records set during a session are folded
/// into that session rather than listed beside it; see GetMyTimelineQuery. A pure read composition
/// over existing append-only tables — no new source of truth.</summary>
public record MyTimelineEntryDto(string Type, DateTimeOffset OccurredAt, string Title, string? Description, string? PhotoUrl);
