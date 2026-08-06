using GymOS.Domain.Common;
using GymOS.Domain.Members;
using Shouldly;

namespace GymOS.Domain.Tests.Members;

/// <summary>
/// Reading today's visit off the turnstile. The point of every test here is that the app should
/// already know the member trained — and should never claim they are somewhere they are not.
///
/// Clock fixed to Thursday 2026-08-06, 18:00 UTC.
/// </summary>
public class VisitPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 6, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 8, 6);

    private static (DateTimeOffset, DateTimeOffset?) Visit(int hour, int? outHour = null) =>
        (new DateTimeOffset(2026, 8, 6, hour, 0, 0, TimeSpan.Zero),
         outHour is int h ? new DateTimeOffset(2026, 8, 6, h, 0, 0, TimeSpan.Zero) : null);

    [Fact]
    public void No_check_in_today_is_no_visit()
    {
        var visit = VisitPolicy.Classify([], [], Now, TimeZoneInfo.Utc);

        visit.State.ShouldBe(VisitState.None);
        visit.CheckedInAt.ShouldBeNull();
        visit.NeedsRecording.ShouldBeFalse();
    }

    [Fact]
    public void An_open_check_in_means_the_member_is_here_now()
    {
        var visit = VisitPolicy.Classify([Visit(17)], [], Now, TimeZoneInfo.Utc);

        visit.State.ShouldBe(VisitState.InGym);
        visit.CheckedInAt.ShouldBe(new DateTimeOffset(2026, 8, 6, 17, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void A_closed_check_in_means_they_came_and_went()
    {
        VisitPolicy.Classify([Visit(9, 10)], [], Now, TimeZoneInfo.Utc).State.ShouldBe(VisitState.Visited);
    }

    [Fact]
    public void A_visit_with_nothing_written_down_is_the_gap_worth_closing()
    {
        var visit = VisitPolicy.Classify([Visit(9, 10)], [], Now, TimeZoneInfo.Utc);

        visit.SessionRecorded.ShouldBeFalse();
        visit.NeedsRecording.ShouldBeTrue();
    }

    [Fact]
    public void A_visit_already_written_down_needs_nothing()
    {
        var visit = VisitPolicy.Classify([Visit(9, 10)], [Today], Now, TimeZoneInfo.Utc);

        visit.SessionRecorded.ShouldBeTrue();
        visit.NeedsRecording.ShouldBeFalse();
    }

    [Fact]
    public void A_check_out_nobody_ever_recorded_does_not_park_the_member_in_the_gym_forever()
    {
        // Gyms are full of visits left open. Yesterday's is yesterday's, however it ended.
        var stale = (new DateTimeOffset(2026, 8, 5, 9, 0, 0, TimeSpan.Zero), (DateTimeOffset?)null);

        var visit = VisitPolicy.Classify([stale], [], Now, TimeZoneInfo.Utc);

        visit.State.ShouldBe(VisitState.None);
        visit.NeedsRecording.ShouldBeFalse();
    }

    [Fact]
    public void Coming_back_a_second_time_means_they_are_here_now_not_that_they_left()
    {
        // Morning session closed, evening session still open.
        var visit = VisitPolicy.Classify([Visit(7, 8), Visit(17)], [], Now, TimeZoneInfo.Utc);

        visit.State.ShouldBe(VisitState.InGym);
        visit.CheckedInAt.ShouldBe(new DateTimeOffset(2026, 8, 6, 17, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Two_finished_visits_report_the_most_recent()
    {
        var visit = VisitPolicy.Classify([Visit(7, 8), Visit(12, 13)], [], Now, TimeZoneInfo.Utc);

        visit.State.ShouldBe(VisitState.Visited);
        visit.CheckedInAt.ShouldBe(new DateTimeOffset(2026, 8, 6, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void A_session_logged_on_another_day_does_not_cover_todays_visit()
    {
        var visit = VisitPolicy.Classify([Visit(9, 10)], [Today.AddDays(-1)], Now, TimeZoneInfo.Utc);

        visit.NeedsRecording.ShouldBeTrue();
    }

    [Fact]
    public void An_evening_visit_is_read_on_the_gyms_clock_not_utc()
    {
        // Trained 10am Thursday in New York, opens the app that evening at 8:30pm. Both moments are
        // plainly Thursday to the member — but the evening one has already crossed into Friday UTC,
        // so on a UTC day the visit and "today" fall on different dates and the app tells someone who
        // trained this morning that they have not been in. That is the exact person the prompt is for.
        var newYork = GymDay.ZoneOrUtc("America/New_York");
        var morningVisit = new DateTimeOffset(2026, 8, 6, 10, 0, 0, TimeSpan.FromHours(-4));
        var thatEvening = new DateTimeOffset(2026, 8, 6, 20, 30, 0, TimeSpan.FromHours(-4));

        VisitPolicy.Classify([(morningVisit, morningVisit.AddHours(1))], [], thatEvening, newYork)
            .State.ShouldBe(VisitState.Visited);
        VisitPolicy.Classify([(morningVisit, morningVisit.AddHours(1))], [], thatEvening, TimeZoneInfo.Utc)
            .State.ShouldBe(VisitState.None); // what it used to do
    }

    [Fact]
    public void Logging_without_ever_checking_in_is_not_treated_as_a_visit()
    {
        // Training elsewhere still counts as a session; it just is not a gym visit.
        var visit = VisitPolicy.Classify([], [Today], Now, TimeZoneInfo.Utc);

        visit.State.ShouldBe(VisitState.None);
        visit.SessionRecorded.ShouldBeTrue();
        visit.NeedsRecording.ShouldBeFalse();
    }
}
