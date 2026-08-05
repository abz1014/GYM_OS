using GymOS.Domain.Members;
using Shouldly;

namespace GymOS.Domain.Tests.Members;

/// <summary>
/// The weekly ring is the first thing a member sees, so the rules behind it are pinned here.
/// 2026-08-05 is a Wednesday; its Monday-start week runs Mon 2026-08-03 .. Sun 2026-08-09.
/// </summary>
public class WeeklyGoalPolicyTests
{
    private static readonly DateOnly Wednesday = new(2026, 8, 5);

    [Fact]
    public void Sessions_this_week_counts_only_days_inside_the_monday_start_week()
    {
        DateOnly[] dates =
        [
            new(2026, 8, 2),  // Sunday — belongs to the PREVIOUS week
            new(2026, 8, 3),  // Monday — first day of this week
            new(2026, 8, 5),  // today
            new(2026, 8, 9),  // Sunday — last day of this week
            new(2026, 8, 10)  // next Monday — the following week
        ];

        WeeklyGoalPolicy.SessionsThisWeek(dates, Wednesday).ShouldBe(3);
    }

    [Fact]
    public void A_day_counts_once_however_many_workouts_were_logged_on_it()
    {
        // Lifting and cardio logged separately on the same day is one day trained, not two.
        DateOnly[] dates = [new(2026, 8, 3), new(2026, 8, 3), new(2026, 8, 3)];

        WeeklyGoalPolicy.SessionsThisWeek(dates, Wednesday).ShouldBe(1);
    }

    [Fact]
    public void No_logged_sessions_means_zero()
        => WeeklyGoalPolicy.SessionsThisWeek([], Wednesday).ShouldBe(0);

    [Fact]
    public void Week_boundary_agrees_with_the_streak_calculator_on_every_day_of_the_week()
    {
        // The ring and the streak flame sit next to each other on the home screen; if these two ever
        // disagreed about where the week starts, the screen would tell two stories about one week.
        var monday = new DateOnly(2026, 8, 3);

        foreach (var offset in Enumerable.Range(0, 7))
        {
            var day = monday.AddDays(offset);
            StreakCalculator.WeekStart(day).ShouldBe(monday);
            WeeklyGoalPolicy.SessionsThisWeek([monday], day).ShouldBe(1);
        }
    }

    [Fact]
    public void On_sunday_the_week_still_reaches_back_to_its_monday()
        => WeeklyGoalPolicy.SessionsThisWeek([new DateOnly(2026, 8, 3)], new DateOnly(2026, 8, 9)).ShouldBe(1);

    [Theory]
    [InlineData(0, 3, 3)]
    [InlineData(1, 3, 2)]
    [InlineData(3, 3, 0)]
    [InlineData(5, 3, 0)]   // overshoot never reads as negative
    public void Remaining_sessions_never_goes_below_zero(int sessions, int goal, int expected)
        => WeeklyGoalPolicy.RemainingSessions(sessions, goal).ShouldBe(expected);

    [Theory]
    [InlineData(2, 3, false)]
    [InlineData(3, 3, true)]
    [InlineData(4, 3, true)]
    public void Goal_is_met_at_or_above_the_target(int sessions, int goal, bool expected)
        => WeeklyGoalPolicy.IsGoalMet(sessions, goal).ShouldBe(expected);

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(3, true)]
    [InlineData(14, true)]
    [InlineData(15, false)]
    [InlineData(-1, false)]
    public void Valid_goals_run_from_one_to_fourteen(int goal, bool expected)
        => WeeklyGoalPolicy.IsValidGoal(goal).ShouldBe(expected);

    [Fact]
    public void The_default_goal_is_itself_a_valid_goal()
        => WeeklyGoalPolicy.IsValidGoal(WeeklyGoalPolicy.DefaultWeeklySessionGoal).ShouldBeTrue();
}
