namespace GymOS.Application.Modules.Experience.Dtos;

/// <summary>The member's progression snapshot: current level, total XP, progress within the current
/// level, and their most recent ledger entries (for a "recent activity" strip).</summary>
public record MyExperienceDto(
    int Level,
    long TotalXp,
    long XpIntoLevel,
    long XpForNextLevel,
    IReadOnlyList<MyXpEntryDto> Recent,
    MyRankDto Rank);

/// <summary>
/// A member's standing on the named ladder — the thing "Level 7" could never say.
///
/// Peak and Current are separate because they answer different questions. Peak is what you reached
/// and cannot be taken back; Current is where you stand and falls with absence. They are equal for
/// anyone who has trained in the last fortnight, which is most people most of the time.
/// </summary>
/// <param name="Peak">Highest tier ever held.</param>
/// <param name="Current">Tier held today.</param>
/// <param name="TiersLostToAbsence">Rungs absence has cost; 0 when Peak and Current agree.</param>
/// <param name="Next">The rung above Peak, or null at Legend.</param>
/// <param name="XpIntoTier">XP earned into the current band.</param>
/// <param name="TierSpan">Size of that band, so a bar can be drawn without the client knowing the
/// thresholds. 0 at Legend, where there is nothing left to fill.</param>
/// <param name="XpToNextTier">XP still needed to reach <paramref name="Next"/>; 0 at Legend.</param>
/// <param name="DaysUntilNextDemotion">Days until absence costs another rung, or null while the
/// member is inside the grace period or has never trained. Shown so a drop is never a surprise.</param>
public record MyRankDto(
    string Peak,
    string Current,
    int TiersLostToAbsence,
    string? Next,
    long XpIntoTier,
    long TierSpan,
    long XpToNextTier,
    int? DaysUntilNextDemotion,
    IReadOnlyList<MyRankPromotionDto> Promotions,
    MyRankPromotionDto? Unseen);

/// <summary>One rung reached, and when. Kept so a member can look back at the climb rather than
/// catching a banner once and never seeing it again.</summary>
public record MyRankPromotionDto(Guid Id, string Tier, DateTimeOffset AchievedAt, bool Seen);

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
    IReadOnlyList<MuscleRecoveryDto> MuscleGroups,
    MyRecoveryTodayDto? Today);

/// <summary>
/// What the member logged for recovery today, or null if they have not logged one.
///
/// Exists because <see cref="MyRecoveryDto"/> could previously report "2 rest days / 7d" without ever
/// saying whether one of them was TODAY — and LogMyRecoveryCommand allows only one log per day,
/// returning the existing row for a second attempt. The screen therefore kept offering "Log recovery
/// day" to someone who already had, and reported success each time, for a record it did not create.
///
/// Kind and Notes are both surfaced because both are stored and neither was ever readable: the member
/// could write a note and never see it again.
/// </summary>
public record MyRecoveryTodayDto(string Kind, string? Notes);

/// <summary>
/// One muscle group's recovery state.
/// </summary>
/// <param name="MuscleGroup">The canonical display name — "Legs", never the gym's "Quads".</param>
/// <param name="MuscleGroupKey">The stable key from MuscleGroupVocabulary. The body map shades by
/// THIS, not by the display name: the map used to match hard-coded zone names against free text a
/// gym owner typed, which agreed only because the seeder happened to spell them the same way.</param>
public record MuscleRecoveryDto(
    string MuscleGroup,
    string MuscleGroupKey,
    string Status,
    string Reason,
    int TimesLast7Days,
    int? DaysSinceLastTrained);

/// <summary>One entry in a member's story — sessions, measurements, photos, achieved goals and
/// unlocked achievements, ordered by when they happened. Records set during a session are folded
/// into that session rather than listed beside it; see GetMyTimelineQuery. A pure read composition
/// over existing append-only tables — no new source of truth.</summary>
public record MyTimelineEntryDto(string Type, DateTimeOffset OccurredAt, string Title, string? Description, string? PhotoUrl);
