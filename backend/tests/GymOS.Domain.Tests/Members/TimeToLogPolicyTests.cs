using GymOS.Domain.Members;
using Shouldly;

namespace GymOS.Domain.Tests.Members;

/// <summary>
/// Time-to-log's edges: the calendar boundary that decides same-day from next-day, the long tail the
/// median exists to survive, and the pairs that cannot be timed at all.
/// </summary>
public class TimeToLogPolicyTests
{
    private static readonly DateOnly Monday = new(2026, 3, 2);

    [Fact]
    public void A_record_made_before_leaving_is_within_the_hour()
    {
        TimeToLogPolicy.Bucket(Monday, Monday, minutesElapsed: 45).ShouldBe(LogLatencyBucket.WithinTheHour);
    }

    [Fact]
    public void A_record_made_later_the_same_evening_is_same_day()
    {
        TimeToLogPolicy.Bucket(Monday, Monday, minutesElapsed: 300).ShouldBe(LogLatencyBucket.SameDay);
    }

    [Fact]
    public void A_late_night_session_recorded_after_midnight_is_next_day_not_within_the_hour()
    {
        // 40 minutes elapsed, but the calendar rolled over. The bucket follows the calendar because
        // "did they record it before going to bed" is the behaviour being asked about, and a member
        // would call this the next day even though it was forty minutes later.
        TimeToLogPolicy.Bucket(Monday, Monday.AddDays(1), minutesElapsed: 40).ShouldBe(LogLatencyBucket.NextDay);
    }

    [Fact]
    public void Two_days_or_more_is_later()
    {
        TimeToLogPolicy.Bucket(Monday, Monday.AddDays(2), minutesElapsed: 3000).ShouldBe(LogLatencyBucket.Later);
        TimeToLogPolicy.Bucket(Monday, Monday.AddDays(30), minutesElapsed: 43_000).ShouldBe(LogLatencyBucket.Later);
    }

    [Fact]
    public void The_median_is_not_dragged_by_one_member_backfilling_a_month()
    {
        // Four prompt records and one month-late one. A mean would report over two hours; the median
        // stays where the behaviour actually is, which is the entire reason it is the median.
        var latencies = new[] { 20, 25, 30, 35, 43_200 };

        TimeToLogPolicy.MedianMinutes(latencies).ShouldBe(30);
    }

    [Fact]
    public void An_even_number_of_samples_averages_the_middle_pair()
    {
        TimeToLogPolicy.MedianMinutes(new[] { 10, 20, 30, 50 }).ShouldBe(25);
    }

    [Fact]
    public void There_is_no_median_of_nothing()
    {
        // Null, not 0 — zero would read as "recorded instantly".
        TimeToLogPolicy.MedianMinutes(Array.Empty<int>()).ShouldBeNull();
    }

    [Fact]
    public void A_workout_logged_before_that_days_check_in_cannot_be_timed()
    {
        var checkIn = new DateTimeOffset(2026, 3, 2, 18, 0, 0, TimeSpan.Zero);
        var loggedThatMorning = new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.Zero);

        // A home workout recorded in the morning before an evening gym visit. Real, unremarkable, and
        // says nothing about how long recording took after arriving.
        TimeToLogPolicy.IsMeasurable(checkIn, loggedThatMorning).ShouldBeFalse();
        TimeToLogPolicy.IsMeasurable(checkIn, checkIn.AddMinutes(50)).ShouldBeTrue();
    }
}
