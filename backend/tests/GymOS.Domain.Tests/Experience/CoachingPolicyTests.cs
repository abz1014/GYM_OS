using GymOS.Domain.Experience;
using Shouldly;

namespace GymOS.Domain.Tests.Experience;

/// <summary>
/// The coaching-dashboard heuristics: adherence-as-percentage for workouts and nutrition, and the
/// "streak about to lapse" flag. All three are pure reductions over pre-computed date lists, so the
/// rule is pinned here independent of how the bulk queries assemble those lists.
/// </summary>
public class CoachingPolicyTests
{
    private static readonly DateOnly Today = new(2026, 8, 5); // a Wednesday

    [Fact]
    public void WorkoutAdherence_is_100_percent_when_every_trailing_week_has_a_session()
    {
        var dates = new[]
        {
            Today, // this week
            Today.AddDays(-7), // last week
            Today.AddDays(-14),
            Today.AddDays(-21),
        };

        CoachingPolicy.WorkoutAdherencePercent(dates, Today).ShouldBe(100);
    }

    [Fact]
    public void WorkoutAdherence_is_0_percent_with_no_history()
    {
        CoachingPolicy.WorkoutAdherencePercent([], Today).ShouldBe(0);
    }

    [Fact]
    public void WorkoutAdherence_counts_each_week_at_most_once_regardless_of_session_count()
    {
        // Five sessions all in the current week, nothing in the prior three -> 1 of 4 weeks = 25%.
        var dates = Enumerable.Range(0, 5).Select(Today.AddDays).ToList();

        CoachingPolicy.WorkoutAdherencePercent(dates, Today).ShouldBe(25);
    }

    [Fact]
    public void WorkoutAdherence_is_50_percent_with_two_of_four_trailing_weeks_active()
    {
        var dates = new[] { Today, Today.AddDays(-14) };

        CoachingPolicy.WorkoutAdherencePercent(dates, Today).ShouldBe(50);
    }

    [Fact]
    public void NutritionAdherence_is_null_when_the_member_never_had_a_diet_plan()
    {
        var signals = new CoachingPolicy.NutritionAdherenceSignals(false, [], [], []);

        CoachingPolicy.NutritionAdherencePercent(signals).ShouldBeNull();
    }

    [Fact]
    public void NutritionAdherence_is_null_when_the_plan_had_no_active_days_in_the_window()
    {
        // Assigned a plan at some point, but it doesn't overlap the trailing window being scored.
        var signals = new CoachingPolicy.NutritionAdherenceSignals(true, [], [], []);

        CoachingPolicy.NutritionAdherencePercent(signals).ShouldBeNull();
    }

    [Fact]
    public void NutritionAdherence_is_the_percentage_of_active_plan_days_actually_logged()
    {
        var planDates = new[] { Today, Today.AddDays(-1), Today.AddDays(-2), Today.AddDays(-3) };
        var loggedDates = new[] { Today, Today.AddDays(-2) }; // logged 2 of the 4 active days

        var signals = new CoachingPolicy.NutritionAdherenceSignals(true, planDates, loggedDates, []);

        CoachingPolicy.NutritionAdherencePercent(signals).ShouldBe(50);
    }

    [Fact]
    public void NutritionAdherence_ignores_a_logged_day_outside_the_active_plan_window()
    {
        var planDates = new[] { Today };
        var loggedDates = new[] { Today, Today.AddDays(-5) }; // the extra day isn't a plan-active day

        var signals = new CoachingPolicy.NutritionAdherenceSignals(true, planDates, loggedDates, []);

        CoachingPolicy.NutritionAdherencePercent(signals).ShouldBe(100);
    }

    [Theory]
    [InlineData(false, 3, true)]  // hasn't visited this week, but last week's visit kept the streak alive -> imminent
    [InlineData(true, 3, false)]  // already visited this week -> not at risk
    [InlineData(false, 0, false)] // no streak to lose in the first place
    public void IsStreakBreakImminent_flags_only_a_live_streak_with_no_visit_yet_this_week(
        bool visitedThisWeek, int currentStreak, bool expected)
        => CoachingPolicy.IsStreakBreakImminent(visitedThisWeek, currentStreak).ShouldBe(expected);
}
