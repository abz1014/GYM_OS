using GymOS.Domain.Experience;
using Shouldly;

namespace GymOS.Domain.Tests.Experience;

/// <summary>The achievement catalog is the deterministic rule set behind every badge; these pin which
/// codes a given stats snapshot earns, and that thresholds are inclusive and independent.</summary>
public class AchievementCatalogTests
{
    [Fact]
    public void A_blank_member_has_earned_nothing()
        => AchievementCatalog.EarnedCodes(new AchievementStats(0, 0, 1, 0, 0)).ShouldBeEmpty();

    [Fact]
    public void First_steps_unlock_the_bronze_milestones_only()
    {
        var earned = AchievementCatalog.EarnedCodes(new AchievementStats(WorkoutCount: 1, VisitCount: 1, Level: 1, PersonalRecordCount: 1, DistinctMuscleGroups: 2)).ToList();

        earned.ShouldBe(["first-workout", "first-visit", "first-pr"], ignoreOrder: true);
        earned.ShouldNotContain("level-3");        // level 1
        earned.ShouldNotContain("balanced-athlete"); // only 2 muscle groups
    }

    [Fact]
    public void A_seasoned_member_earns_the_full_ladder_up_to_their_thresholds()
    {
        var earned = AchievementCatalog.EarnedCodes(new AchievementStats(WorkoutCount: 10, VisitCount: 25, Level: 5, PersonalRecordCount: 10, DistinctMuscleGroups: 4)).ToList();

        earned.ShouldContain("workouts-10");
        earned.ShouldContain("visits-25");
        earned.ShouldContain("level-3");
        earned.ShouldContain("level-5");
        earned.ShouldContain("pr-10");
        earned.ShouldContain("balanced-athlete");
        // Not yet the top tiers.
        earned.ShouldNotContain("workouts-50");
        earned.ShouldNotContain("visits-100");
        earned.ShouldNotContain("level-10");
    }

    [Fact]
    public void Growing_every_stat_only_ever_adds_achievements()
    {
        var fewer = AchievementCatalog.EarnedCodes(new AchievementStats(5, 10, 3, 3, 3)).ToHashSet();
        var more = AchievementCatalog.EarnedCodes(new AchievementStats(50, 100, 10, 10, 5)).ToHashSet();

        fewer.IsSubsetOf(more).ShouldBeTrue();
    }

    [Fact]
    public void Every_catalog_code_is_unique()
    {
        var codes = AchievementCatalog.All.Select(a => a.Code).ToList();
        codes.Distinct().Count().ShouldBe(codes.Count);
    }

    [Fact]
    public void Completing_a_challenge_unlocks_the_challenge_achievement_only()
    {
        var earned = AchievementCatalog.EarnedCodes(new AchievementStats(0, 0, 1, 0, 0, ChallengesCompleted: 1)).ToList();

        earned.ShouldBe(["first-challenge"]);
    }

    [Fact]
    public void ChallengesCompleted_defaults_to_zero_so_existing_callers_are_unaffected()
        => AchievementCatalog.EarnedCodes(new AchievementStats(0, 0, 1, 0, 0)).ShouldNotContain("first-challenge");
}
