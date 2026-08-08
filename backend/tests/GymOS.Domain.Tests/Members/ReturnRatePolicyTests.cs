using GymOS.Domain.Members;
using Shouldly;

namespace GymOS.Domain.Tests.Members;

/// <summary>
/// The rules that decide who counts. Week-N return is arithmetic; the part worth testing is which
/// members are allowed into the denominator, because that is where the number goes quietly wrong.
/// </summary>
public class ReturnRatePolicyTests
{
    private static readonly DateOnly Today = new(2026, 6, 1);

    [Fact]
    public void Week_one_is_the_joining_week_itself()
    {
        var (start, end) = ReturnRatePolicy.WeekWindow(new DateOnly(2026, 1, 5), 1);

        start.ShouldBe(new DateOnly(2026, 1, 5));
        end.ShouldBe(new DateOnly(2026, 1, 11));
    }

    [Fact]
    public void Week_two_begins_seven_days_after_joining()
    {
        var (start, end) = ReturnRatePolicy.WeekWindow(new DateOnly(2026, 1, 5), 2);

        start.ShouldBe(new DateOnly(2026, 1, 12));
        end.ShouldBe(new DateOnly(2026, 1, 18));
    }

    [Fact]
    public void A_member_who_joined_days_ago_is_not_counted_as_having_failed_week_twelve()
    {
        // The whole reason ReturnRatePolicy separates eligibility from outcome. This member has not
        // had a week 12; counting them would make a growing gym look like a churning one.
        var joinedThreeDaysAgo = Today.AddDays(-3);

        ReturnRatePolicy.IsEligibleForWeek(joinedThreeDaysAgo, Today, 12).ShouldBeFalse();
    }

    [Fact]
    public void A_member_is_eligible_only_once_their_week_has_fully_elapsed()
    {
        // Week 2 covers days 7-13. Joining exactly 13 days ago means today IS their last day, so the
        // answer is not in yet; 14 days ago means the window closed yesterday.
        var stillInsideWeekTwo = Today.AddDays(-13);
        var justPastWeekTwo = Today.AddDays(-14);

        ReturnRatePolicy.IsEligibleForWeek(stillInsideWeekTwo, Today, 2).ShouldBeFalse();
        ReturnRatePolicy.IsEligibleForWeek(justPastWeekTwo, Today, 2).ShouldBeTrue();
    }

    [Fact]
    public void A_visit_outside_the_week_does_not_count_as_returning_in_it()
    {
        var joined = new DateOnly(2026, 1, 5);
        // Day 6 is still week 1, day 14 is already week 3 — neither is week 2.
        var visits = new[] { new DateOnly(2026, 1, 11), new DateOnly(2026, 1, 19) };

        ReturnRatePolicy.ReturnedInWeek(joined, visits, 2).ShouldBeFalse();
    }

    [Fact]
    public void A_visit_inside_the_week_counts_once_however_many_times_they_came()
    {
        var joined = new DateOnly(2026, 1, 5);
        var visits = new[] { new DateOnly(2026, 1, 13), new DateOnly(2026, 1, 14), new DateOnly(2026, 1, 15) };

        ReturnRatePolicy.ReturnedInWeek(joined, visits, 2).ShouldBeTrue();
    }

    [Fact]
    public void An_empty_cohort_reports_zero_rather_than_dividing_by_zero()
    {
        ReturnRatePolicy.RatePercent(returned: 0, eligible: 0).ShouldBe(0);
    }

    [Fact]
    public void Sessions_per_member_per_week_is_null_when_nobody_visited()
    {
        // Not 0.0 — that would claim the members trained zero times rather than that there were none.
        ReturnRatePolicy.SessionsPerMemberPerWeek(loggedSessions: 0, membersWhoVisited: 0, weeks: 12).ShouldBeNull();
    }

    [Fact]
    public void Sessions_per_member_per_week_divides_by_both_members_and_weeks()
    {
        // 240 sessions, 20 members, 4 weeks -> 3 sessions each per week.
        ReturnRatePolicy.SessionsPerMemberPerWeek(240, 20, 4).ShouldBe(3.0);
    }
}
