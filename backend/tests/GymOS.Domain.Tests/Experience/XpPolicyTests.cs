using GymOS.Domain.Experience;
using Shouldly;

namespace GymOS.Domain.Tests.Experience;

/// <summary>
/// The XP curve and award table are pure, so the whole progression system's arithmetic is pinned
/// here — level thresholds, in-level progress, and the "consistency/verification over raw load"
/// weighting the blueprint requires.
/// </summary>
public class XpPolicyTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(99, 1)]
    [InlineData(100, 2)]   // 50·1·2
    [InlineData(299, 2)]
    [InlineData(300, 3)]   // 50·2·3
    [InlineData(599, 3)]
    [InlineData(600, 4)]   // 50·3·4
    [InlineData(1000, 5)]  // 50·4·5
    public void LevelForXp_crosses_at_the_cumulative_thresholds(long totalXp, int expectedLevel)
        => XpPolicy.LevelForXp(totalXp).Level.ShouldBe(expectedLevel);

    [Fact]
    public void LevelForXp_reports_progress_within_the_current_level()
    {
        // 150 XP = level 2 (>=100, <300); 50 into the band, band size 100·2 = 200.
        var (level, into, forNext) = XpPolicy.LevelForXp(150);

        level.ShouldBe(2);
        into.ShouldBe(50);
        forNext.ShouldBe(200);
    }

    [Fact]
    public void Negative_or_zero_xp_is_a_clean_level_1()
    {
        XpPolicy.LevelForXp(0).ShouldBe((1, 0L, 100L));
        XpPolicy.LevelForXp(-5).Level.ShouldBe(1);
    }

    [Fact]
    public void The_level_curve_is_monotonic_and_consistent_with_the_award_table()
    {
        // Every successive threshold is strictly higher, and the in-level band equals 100·level.
        for (var level = 1; level <= 50; level++)
        {
            XpPolicy.CumulativeXpForLevel(level + 1).ShouldBeGreaterThan(XpPolicy.CumulativeXpForLevel(level));
            (XpPolicy.CumulativeXpForLevel(level + 1) - XpPolicy.CumulativeXpForLevel(level)).ShouldBe(100L * level);
        }
    }

    [Fact]
    public void Award_weights_consistency_and_verification_above_a_bare_workout()
    {
        // "Never reward unsafe lifting alone": a raw logged workout is worth less than trainer
        // verification, goal completion, or finishing a challenge.
        var workout = XpPolicy.AwardFor(XpReason.WorkoutCompleted);

        workout.ShouldBeGreaterThan(0);
        XpPolicy.AwardFor(XpReason.TrainerVerified).ShouldBeGreaterThan(workout);
        XpPolicy.AwardFor(XpReason.GoalCompleted).ShouldBeGreaterThan(workout);
        XpPolicy.AwardFor(XpReason.ChallengeCompleted).ShouldBeGreaterThan(XpPolicy.AwardFor(XpReason.GoalCompleted));
    }
}
