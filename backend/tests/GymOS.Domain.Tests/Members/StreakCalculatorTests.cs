using GymOS.Domain.Members;
using Shouldly;

namespace GymOS.Domain.Tests.Members;

/// <summary>
/// The streak is the single number a member sees first on their progress page, so its edge cases
/// (rest days, a week still in progress, a genuinely missed week) are pinned here. 2026-08-04 is a
/// Tuesday; that week's Monday is 2026-08-03.
/// </summary>
public class StreakCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 8, 4); // Tuesday

    [Fact]
    public void Week_start_is_the_monday_of_that_week()
    {
        StreakCalculator.WeekStart(new DateOnly(2026, 8, 4)).ShouldBe(new DateOnly(2026, 8, 3));  // Tue -> Mon
        StreakCalculator.WeekStart(new DateOnly(2026, 8, 9)).ShouldBe(new DateOnly(2026, 8, 3));  // Sun -> same Mon
        StreakCalculator.WeekStart(new DateOnly(2026, 8, 3)).ShouldBe(new DateOnly(2026, 8, 3));  // Mon -> itself
    }

    [Fact]
    public void No_visits_means_no_streak()
    {
        StreakCalculator.CurrentWeeklyStreak([], Today).ShouldBe(0);
    }

    [Fact]
    public void Multiple_visits_in_one_week_count_as_one_streak_week()
    {
        var visits = new[] { new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 4) };

        StreakCalculator.CurrentWeeklyStreak(visits, Today).ShouldBe(1);
    }

    [Fact]
    public void Consecutive_weeks_accumulate()
    {
        // Visits in the current week and each of the two weeks before it.
        var visits = new[] { new DateOnly(2026, 8, 3), new DateOnly(2026, 7, 29), new DateOnly(2026, 7, 21) };

        StreakCalculator.CurrentWeeklyStreak(visits, Today).ShouldBe(3);
    }

    [Fact]
    public void A_week_in_progress_without_a_visit_yet_does_not_break_the_streak()
    {
        // It's Tuesday and the member hasn't been in this week — but they came last week and the
        // week before, so the streak stands at 2 rather than snapping to 0 mid-week.
        var visits = new[] { new DateOnly(2026, 7, 30), new DateOnly(2026, 7, 22) };

        StreakCalculator.CurrentWeeklyStreak(visits, Today).ShouldBe(2);
    }

    [Fact]
    public void A_fully_missed_week_breaks_the_streak()
    {
        // Last visit two weeks ago — all of last week was missed, so the streak is gone.
        var visits = new[] { new DateOnly(2026, 7, 22), new DateOnly(2026, 7, 15) };

        StreakCalculator.CurrentWeeklyStreak(visits, Today).ShouldBe(0);
    }

    [Fact]
    public void A_gap_further_back_only_ends_the_count_not_the_current_streak()
    {
        // Current week + last week visited, then a missed week, then older history: streak is 2.
        var visits = new[] { new DateOnly(2026, 8, 4), new DateOnly(2026, 7, 28), new DateOnly(2026, 7, 8) };

        StreakCalculator.CurrentWeeklyStreak(visits, Today).ShouldBe(2);
    }
}
