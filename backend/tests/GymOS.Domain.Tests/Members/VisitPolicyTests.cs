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
        var visit = VisitPolicy.Classify([], [], Now);

        visit.State.ShouldBe(VisitState.None);
        visit.CheckedInAt.ShouldBeNull();
        visit.NeedsRecording.ShouldBeFalse();
    }

    [Fact]
    public void An_open_check_in_means_the_member_is_here_now()
    {
        var visit = VisitPolicy.Classify([Visit(17)], [], Now);

        visit.State.ShouldBe(VisitState.InGym);
        visit.CheckedInAt.ShouldBe(new DateTimeOffset(2026, 8, 6, 17, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void A_closed_check_in_means_they_came_and_went()
    {
        VisitPolicy.Classify([Visit(9, 10)], [], Now).State.ShouldBe(VisitState.Visited);
    }

    [Fact]
    public void A_visit_with_nothing_written_down_is_the_gap_worth_closing()
    {
        var visit = VisitPolicy.Classify([Visit(9, 10)], [], Now);

        visit.SessionRecorded.ShouldBeFalse();
        visit.NeedsRecording.ShouldBeTrue();
    }

    [Fact]
    public void A_visit_already_written_down_needs_nothing()
    {
        var visit = VisitPolicy.Classify([Visit(9, 10)], [Today], Now);

        visit.SessionRecorded.ShouldBeTrue();
        visit.NeedsRecording.ShouldBeFalse();
    }

    [Fact]
    public void A_check_out_nobody_ever_recorded_does_not_park_the_member_in_the_gym_forever()
    {
        // Gyms are full of visits left open. Yesterday's is yesterday's, however it ended.
        var stale = (new DateTimeOffset(2026, 8, 5, 9, 0, 0, TimeSpan.Zero), (DateTimeOffset?)null);

        var visit = VisitPolicy.Classify([stale], [], Now);

        visit.State.ShouldBe(VisitState.None);
        visit.NeedsRecording.ShouldBeFalse();
    }

    [Fact]
    public void Coming_back_a_second_time_means_they_are_here_now_not_that_they_left()
    {
        // Morning session closed, evening session still open.
        var visit = VisitPolicy.Classify([Visit(7, 8), Visit(17)], [], Now);

        visit.State.ShouldBe(VisitState.InGym);
        visit.CheckedInAt.ShouldBe(new DateTimeOffset(2026, 8, 6, 17, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Two_finished_visits_report_the_most_recent()
    {
        var visit = VisitPolicy.Classify([Visit(7, 8), Visit(12, 13)], [], Now);

        visit.State.ShouldBe(VisitState.Visited);
        visit.CheckedInAt.ShouldBe(new DateTimeOffset(2026, 8, 6, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void A_session_logged_on_another_day_does_not_cover_todays_visit()
    {
        var visit = VisitPolicy.Classify([Visit(9, 10)], [Today.AddDays(-1)], Now);

        visit.NeedsRecording.ShouldBeTrue();
    }

    [Fact]
    public void Logging_without_ever_checking_in_is_not_treated_as_a_visit()
    {
        // Training elsewhere still counts as a session; it just is not a gym visit.
        var visit = VisitPolicy.Classify([], [Today], Now);

        visit.State.ShouldBe(VisitState.None);
        visit.SessionRecorded.ShouldBeTrue();
        visit.NeedsRecording.ShouldBeFalse();
    }
}
