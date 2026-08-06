using GymOS.Domain.Experience;
using Shouldly;

namespace GymOS.Domain.Tests.Experience;

/// <summary>
/// The leaderboard is the only place this app shows one member to another, so the rules about
/// ranking, ties, privacy and how a mid-table member sees themselves are pinned here.
/// </summary>
public class LeaderboardPolicyTests
{
    private static Guid M(int n) => new($"{n:D8}-0000-0000-0000-000000000000");

    [Fact]
    public void Ranks_highest_score_first()
    {
        var standings = LeaderboardPolicy.Rank([(M(1), 10), (M(2), 30), (M(3), 20)]);

        standings.Select(s => s.MemberId).ShouldBe([M(2), M(3), M(1)]);
        standings.Select(s => s.Rank).ShouldBe([1, 2, 3]);
    }

    [Fact]
    public void Ties_share_a_rank_and_the_next_distinct_score_skips_ahead()
    {
        // Competition ranking: 1, 2, 2, 4 — never 1, 2, 2, 3, which would tell the fourth member
        // they came third.
        var standings = LeaderboardPolicy.Rank([(M(1), 50), (M(2), 40), (M(3), 40), (M(4), 10)]);

        standings.Select(s => s.Rank).ShouldBe([1, 2, 2, 4]);
    }

    [Fact]
    public void Members_who_scored_nothing_are_left_off_entirely()
    {
        var standings = LeaderboardPolicy.Rank([(M(1), 5), (M(2), 0), (M(3), 0)]);

        standings.Count.ShouldBe(1);
        standings.ShouldAllBe(s => s.Score > 0);
    }

    [Fact]
    public void Ordering_is_deterministic_when_scores_are_equal()
    {
        // Same input in a different order must produce the same board, or a member appears to move
        // between refreshes without doing anything.
        var a = LeaderboardPolicy.Rank([(M(1), 20), (M(2), 20), (M(3), 20)]);
        var b = LeaderboardPolicy.Rank([(M(3), 20), (M(1), 20), (M(2), 20)]);

        a.Select(s => s.MemberId).ShouldBe(b.Select(s => s.MemberId));
    }

    [Fact]
    public void Neighbours_show_the_member_in_context()
    {
        var standings = LeaderboardPolicy.Rank(Enumerable.Range(1, 20).Select(i => (M(i), 100 - i)));

        var around = LeaderboardPolicy.NeighboursOf(standings, M(10));

        around.Count.ShouldBe(5);                       // two either side
        around.ShouldContain(s => s.MemberId == M(10));
        around.Select(s => s.MemberId).ShouldBe([M(8), M(9), M(10), M(11), M(12)]);
    }

    [Fact]
    public void Neighbours_clamp_at_the_top_of_the_board()
    {
        var standings = LeaderboardPolicy.Rank(Enumerable.Range(1, 10).Select(i => (M(i), 100 - i)));

        var around = LeaderboardPolicy.NeighboursOf(standings, M(1));

        around.First().MemberId.ShouldBe(M(1));         // nothing above first place
        around.Count.ShouldBe(3);
    }

    [Fact]
    public void Neighbours_are_empty_for_a_member_who_is_not_on_the_board()
    {
        var standings = LeaderboardPolicy.Rank([(M(1), 10)]);

        LeaderboardPolicy.NeighboursOf(standings, M(99)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(1, 100, 100)]   // first place
    [InlineData(100, 100, 1)]   // last place
    [InlineData(50, 100, 51)]
    [InlineData(1, 1, 100)]     // only entrant
    public void Percentile_counts_from_the_top(int rank, int total, int expected)
        => LeaderboardPolicy.PercentileFor(rank, total).ShouldBe(expected);

    [Fact]
    public void Percentile_is_zero_when_there_is_no_board()
        => LeaderboardPolicy.PercentileFor(0, 0).ShouldBe(0);

    [Theory]
    [InlineData("Noah", "Dooley", "Noah D.")]
    [InlineData("Noah", null, "Noah")]
    [InlineData("Noah", "", "Noah")]
    [InlineData("", "Dooley", "Dooley")]
    [InlineData("", null, "Member")]
    [InlineData("noah", "dooley", "noah D.")]   // initial is capitalised even when the data isn't
    public void Display_name_is_first_name_plus_last_initial(string first, string? last, string expected)
        => LeaderboardPolicy.DisplayName(first, last).ShouldBe(expected);

    [Fact]
    public void Display_name_never_leaks_a_full_surname()
    {
        LeaderboardPolicy.DisplayName("Noah", "Dooley").ShouldNotContain("Dooley");
    }

    [Fact]
    public void Month_window_covers_the_calendar_month()
    {
        var (start, end) = LeaderboardPolicy.WindowFor(LeaderboardPeriod.Month, new DateOnly(2026, 8, 6));

        start.ShouldBe(new DateOnly(2026, 8, 1));
        end.ShouldBe(new DateOnly(2026, 8, 31));
    }

    [Fact]
    public void Month_window_handles_february_in_a_leap_year()
    {
        var (start, end) = LeaderboardPolicy.WindowFor(LeaderboardPeriod.Month, new DateOnly(2028, 2, 10));

        start.ShouldBe(new DateOnly(2028, 2, 1));
        end.ShouldBe(new DateOnly(2028, 2, 29));
    }

    [Fact]
    public void Week_window_is_monday_to_sunday_like_every_other_week_here()
    {
        var (start, end) = LeaderboardPolicy.WindowFor(LeaderboardPeriod.Week, new DateOnly(2026, 8, 6)); // Thursday

        start.ShouldBe(new DateOnly(2026, 8, 3));   // Monday
        end.ShouldBe(new DateOnly(2026, 8, 9));     // Sunday
        start.ShouldBe(GymOS.Domain.Members.StreakCalculator.WeekStart(new DateOnly(2026, 8, 6)));
    }
}
