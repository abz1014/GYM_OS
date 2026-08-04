namespace GymOS.Domain.Experience;

/// <summary>The member stats an achievement predicate is evaluated against — all derivable from data
/// the engine already tracks.</summary>
public record AchievementStats(int WorkoutCount, int VisitCount, int Level, int PersonalRecordCount, int DistinctMuscleGroups);

/// <summary>A single achievement's definition: how it reads, its badge tier/icon, and the pure
/// predicate that decides whether a member has earned it.</summary>
public record AchievementDefinition(
    string Code,
    string Name,
    string Description,
    AchievementTier Tier,
    AchievementCategory Category,
    string Icon,
    Func<AchievementStats, bool> IsUnlocked);

/// <summary>
/// The fixed, deterministic achievement catalog — the single source of truth (a code catalog like the
/// other domain policies, not a DB table). Pure: given a stats snapshot it says which achievements are
/// earned; the evaluator diffs that against what a member has already unlocked. Ordered for display.
/// </summary>
public static class AchievementCatalog
{
    public static readonly IReadOnlyList<AchievementDefinition> All =
    [
        new("first-workout", "First Workout", "Logged your very first workout.",
            AchievementTier.Bronze, AchievementCategory.Milestone, "dumbbell", s => s.WorkoutCount >= 1),
        new("workouts-10", "Getting Consistent", "Logged 10 workouts.",
            AchievementTier.Silver, AchievementCategory.Consistency, "dumbbell", s => s.WorkoutCount >= 10),
        new("workouts-50", "Committed", "Logged 50 workouts.",
            AchievementTier.Gold, AchievementCategory.Consistency, "dumbbell", s => s.WorkoutCount >= 50),

        new("first-visit", "Welcome In", "Checked in for the first time.",
            AchievementTier.Bronze, AchievementCategory.Milestone, "door-open", s => s.VisitCount >= 1),
        new("visits-25", "Regular", "25 gym visits.",
            AchievementTier.Silver, AchievementCategory.Consistency, "calendar-check", s => s.VisitCount >= 25),
        new("visits-100", "Century Club", "100 gym visits.",
            AchievementTier.Platinum, AchievementCategory.Consistency, "calendar-check", s => s.VisitCount >= 100),

        new("level-3", "Rising", "Reached level 3.",
            AchievementTier.Bronze, AchievementCategory.Progression, "zap", s => s.Level >= 3),
        new("level-5", "Seasoned", "Reached level 5.",
            AchievementTier.Silver, AchievementCategory.Progression, "zap", s => s.Level >= 5),
        new("level-10", "Elite", "Reached level 10.",
            AchievementTier.Gold, AchievementCategory.Progression, "zap", s => s.Level >= 10),

        new("first-pr", "New Record", "Set your first personal record.",
            AchievementTier.Bronze, AchievementCategory.Strength, "trophy", s => s.PersonalRecordCount >= 1),
        new("pr-10", "Record Breaker", "Set 10 personal records.",
            AchievementTier.Gold, AchievementCategory.Strength, "trophy", s => s.PersonalRecordCount >= 10),
        new("balanced-athlete", "Balanced Athlete", "Trained 4 or more muscle groups.",
            AchievementTier.Silver, AchievementCategory.Strength, "activity", s => s.DistinctMuscleGroups >= 4),
    ];

    /// <summary>The codes a member with these stats should have earned.</summary>
    public static IEnumerable<string> EarnedCodes(AchievementStats stats)
        => All.Where(a => a.IsUnlocked(stats)).Select(a => a.Code);
}
