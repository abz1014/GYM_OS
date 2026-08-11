using GymOS.Domain.Experience;
using Shouldly;

namespace GymOS.Domain.Tests.Experience;

/// <summary>
/// The ladder, and the one rule that separates it from the games it borrows from: a member loses a
/// rung for being ABSENT, never for losing XP, and returning restores them.
/// </summary>
public class RankPolicyTests
{
    [Theory]
    [InlineData(0, RankTier.Newcomer)]
    [InlineData(749, RankTier.Newcomer)]
    [InlineData(750, RankTier.Regular)]
    [InlineData(2_499, RankTier.Regular)]
    [InlineData(2_500, RankTier.Committed)]
    [InlineData(12_000, RankTier.Relentless)]
    [InlineData(50_000, RankTier.Legend)]
    [InlineData(500_000, RankTier.Legend)]
    public void Tier_opens_exactly_at_its_threshold(long xp, RankTier expected) =>
        RankPolicy.TierFor(xp).ShouldBe(expected);

    [Fact]
    public void Legend_is_the_top_and_says_so_rather_than_faking_progress()
    {
        RankPolicy.NextTierAfter(RankTier.Legend).ShouldBeNull();
        RankPolicy.ProgressWithin(60_000).ShouldBe((0L, 0L));
    }

    [Fact]
    public void Progress_is_measured_within_the_band_not_from_zero()
    {
        // 1,000 XP is 250 into the Regular band, which runs 750 -> 2,500.
        RankPolicy.ProgressWithin(1_000).ShouldBe((250L, 1_750L));
    }

    [Fact]
    public void A_member_who_trains_holds_their_peak()
    {
        var s = RankPolicy.StandingFor(6_000, daysSinceLastActivity: 3);

        s.Peak.ShouldBe(RankTier.Strong);
        s.Current.ShouldBe(RankTier.Strong);
        s.TiersLostToAbsence.ShouldBe(0);
    }

    /// <summary>A fortnight covers a holiday, a flu, or a deload — nobody is told they slipped for it.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(41)] // 14 grace + 27: still one day short of a full 28-day step
    public void Absence_inside_the_grace_and_first_step_costs_nothing(int days)
    {
        var s = RankPolicy.StandingFor(6_000, days);

        s.Current.ShouldBe(RankTier.Strong);
        s.TiersLostToAbsence.ShouldBe(0);
    }

    [Theory]
    [InlineData(42, RankTier.Strong, 1)]     // 14 grace + 28 -> one rung
    [InlineData(70, RankTier.Committed, 2)]  // 14 + 56       -> two
    [InlineData(98, RankTier.Regular, 3)]    // 14 + 84       -> three
    public void Each_further_month_away_costs_one_rung(int days, RankTier expected, int expectedLost)
    {
        var s = RankPolicy.StandingFor(12_000, days);

        s.Peak.ShouldBe(RankTier.Relentless, "12,000 XP is the Relentless threshold");
        s.Current.ShouldBe(expected);
        s.TiersLostToAbsence.ShouldBe(expectedLost);
    }

    [Fact]
    public void Absence_can_never_take_more_than_a_member_had()
    {
        // Peak Regular, away the better part of a year: the floor is Newcomer, and the reported loss
        // is what was really lost — one rung — not the six the arithmetic wanted to take.
        var s = RankPolicy.StandingFor(750, daysSinceLastActivity: 300);

        s.Peak.ShouldBe(RankTier.Regular);
        s.Current.ShouldBe(RankTier.Newcomer);
        s.TiersLostToAbsence.ShouldBe(1);
    }

    /// <summary>
    /// The whole point. A member gone half a year is demoted; the session on the day they walk back in
    /// restores them completely. The drop is a pull to return, not a debt to repay — anything else
    /// punishes people at the exact moment they are deciding whether to come back.
    /// </summary>
    [Fact]
    public void Coming_back_restores_the_peak_immediately()
    {
        var lapsed = RankPolicy.StandingFor(20_000, daysSinceLastActivity: 180);
        ((int)lapsed.Current).ShouldBeLessThan((int)lapsed.Peak);

        var returned = RankPolicy.StandingFor(20_000, daysSinceLastActivity: 0);

        returned.Current.ShouldBe(RankTier.Elite);
        returned.Current.ShouldBe(returned.Peak);
        returned.TiersLostToAbsence.ShouldBe(0);
    }

    /// <summary>A member who has never logged anything is not absent — they have not started.</summary>
    [Fact]
    public void A_brand_new_member_is_not_treated_as_lapsed()
    {
        var s = RankPolicy.StandingFor(0, daysSinceLastActivity: null);

        s.Peak.ShouldBe(RankTier.Newcomer);
        s.Current.ShouldBe(RankTier.Newcomer);
        s.TiersLostToAbsence.ShouldBe(0);
    }
}
