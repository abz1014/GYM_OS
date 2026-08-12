using GymOS.Domain.Experience;
using Shouldly;

namespace GymOS.Domain.Tests.Experience;

/// <summary>
/// The two places the rank screen could lie: an ETA computed from a pace that is not really a pace,
/// and a "tip" that is not about this member.
/// </summary>
public class RankClimbPolicyTests
{
    [Fact]
    public void Pace_averages_the_window_into_a_weekly_rate()
    {
        // 1,200 XP over 28 days is four weeks at 300 — the rate RankPolicy's thresholds were
        // calibrated against, so this is the shape of an engaged member.
        var pace = RankClimbPolicy.PaceFor(xpInWindow: 1_200, xpToNextTier: 1_500, atTopOfLadder: false);

        pace.XpPerWeek.ShouldBe(300);
        pace.WeeksToNextTier.ShouldBe(5);
    }

    [Fact]
    public void A_pace_too_small_to_mean_anything_gets_no_estimate()
    {
        // 40 XP in four weeks is 10 a week. The arithmetic gives 550 weeks, which is correct,
        // useless, and worst of all discouraging to somebody who has just started coming back.
        var pace = RankClimbPolicy.PaceFor(xpInWindow: 40, xpToNextTier: 5_500, atTopOfLadder: false);

        pace.XpPerWeek.ShouldBe(10);
        pace.WeeksToNextTier.ShouldBeNull();
    }

    [Fact]
    public void An_estimate_beyond_two_years_is_withheld_rather_than_printed()
    {
        // 30 XP/wk clears the meaningful-pace bar, so the difference here is purely the cap.
        var pace = RankClimbPolicy.PaceFor(xpInWindow: 120, xpToNextTier: 30_000, atTopOfLadder: false);

        pace.XpPerWeek.ShouldBe(30);
        pace.WeeksToNextTier.ShouldBeNull();
    }

    [Fact]
    public void Legend_has_no_estimate_because_there_is_nothing_above_it()
    {
        var pace = RankClimbPolicy.PaceFor(xpInWindow: 4_000, xpToNextTier: 0, atTopOfLadder: true);

        pace.XpPerWeek.ShouldBe(1_000);
        pace.WeeksToNextTier.ShouldBeNull();
    }

    /// <summary>Nothing done, nothing joined — every tip that does not depend on training applies.</summary>
    private static RankClimbPolicy.ClimbActivity Nothing => new(0, 0, 0, 0, 0, false);

    [Fact]
    public void Every_tip_carries_the_award_the_action_really_pays()
    {
        var tips = RankClimbPolicy.TipsFor(Nothing);

        // Not "roughly 100 XP" or a number typed into a string — the same table the engine awards from.
        tips.ShouldContain(t => t.Code == "challenge" && t.XpValue == XpPolicy.AwardFor(XpReason.ChallengeCompleted));
        tips.ShouldContain(t => t.Code == "recovery" && t.XpValue == XpPolicy.AwardFor(XpReason.RecoveryLogged));
    }

    [Fact]
    public void Tips_are_ordered_by_what_they_are_worth()
    {
        var tips = RankClimbPolicy.TipsFor(Nothing with { Workouts = 4 });

        tips.Select(t => t.XpValue).ShouldBe(tips.Select(t => t.XpValue).OrderByDescending(v => v));
        // A screen showing three shows the three worth most.
        tips[0].Code.ShouldBe("challenge");
    }

    [Fact]
    public void The_check_in_tip_counts_the_sessions_that_actually_had_none()
    {
        // Four sessions, one check-in: three were missed, and the tip says three.
        var tip = RankClimbPolicy.TipsFor(Nothing with { Workouts = 4, CheckIns = 1 })
            .Single(t => t.Code == "check-in");

        tip.Detail.ShouldContain("3 of your last 4");
    }

    [Fact]
    public void No_check_in_tip_when_every_session_was_checked_in()
    {
        RankClimbPolicy.TipsFor(Nothing with { Workouts = 3, CheckIns = 3 })
            .ShouldNotContain(t => t.Code == "check-in");
    }

    [Fact]
    public void Training_tips_stay_silent_for_a_member_who_has_not_been_training()
    {
        // Telling somebody who has not been in for a month to add weight to a lift answers a question
        // they are not asking. What they need is the door held open, which is the SlippedCard's job.
        var tips = RankClimbPolicy.TipsFor(Nothing);

        tips.ShouldNotContain(t => t.Code == "check-in");
        tips.ShouldNotContain(t => t.Code == "progress");
    }

    [Fact]
    public void A_member_already_doing_everything_is_told_nothing()
    {
        var doingEverything = new RankClimbPolicy.ClimbActivity(
            Workouts: 8, CheckIns: 8, PersonalRecords: 1, DaysWithMealsLogged: 20, RecoveryDays: 4,
            InActiveChallenge: true);

        RankClimbPolicy.TipsFor(doingEverything).ShouldBeEmpty();
    }
}
