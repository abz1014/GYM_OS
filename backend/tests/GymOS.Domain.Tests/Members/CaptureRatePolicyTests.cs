using GymOS.Domain.Members;
using Shouldly;

namespace GymOS.Domain.Tests.Members;

/// <summary>
/// Capture rate is the number the whole member-experience roadmap is judged on, so its definition —
/// including when it should NOT be trusted — is pinned here rather than left inside a query.
/// </summary>
public class CaptureRatePolicyTests
{
    [Theory]
    [InlineData(0, 100, 0)]
    [InlineData(34, 100, 34)]
    [InlineData(100, 100, 100)]
    [InlineData(1, 3, 33)]
    [InlineData(2, 3, 67)]     // rounds half-up, not toward even
    public void Rate_is_logged_visits_over_visits(int logged, int visits, int expected)
        => CaptureRatePolicy.RatePercent(logged, visits).ShouldBe(expected);

    [Fact]
    public void No_visits_reports_zero_rather_than_dividing_by_zero()
    {
        CaptureRatePolicy.RatePercent(0, 0).ShouldBe(0);
        CaptureRatePolicy.RatePercent(5, 0).ShouldBe(0);
    }

    [Fact]
    public void A_point_computes_its_own_rate()
        => new CapturePoint(new DateOnly(2026, 8, 3), VisitDays: 200, LoggedVisitDays: 68, OrphanLogDays: 4)
            .CaptureRatePercent.ShouldBe(34);

    [Fact]
    public void A_clean_period_is_reliable()
        => CaptureRatePolicy.IsReliable(orphanLogDays: 0, loggedVisitDays: 500).ShouldBeTrue();

    [Fact]
    public void A_few_off_site_logs_are_normal_and_still_reliable()
        // Training at home, a staff correction, a member who forgot to scan in.
        => CaptureRatePolicy.IsReliable(orphanLogDays: 10, loggedVisitDays: 500).ShouldBeTrue();

    [Fact]
    public void Mostly_off_site_logs_mean_the_rate_is_not_describing_gym_behaviour()
        => CaptureRatePolicy.IsReliable(orphanLogDays: 300, loggedVisitDays: 100).ShouldBeFalse();

    [Fact]
    public void An_empty_period_is_not_reported_as_unreliable()
        // Nothing happened; that's not the same as something being wrong.
        => CaptureRatePolicy.IsReliable(0, 0).ShouldBeTrue();

    [Fact]
    public void The_reliability_boundary_is_inclusive()
    {
        CaptureRatePolicy.IsReliable(orphanLogDays: 20, loggedVisitDays: 80).ShouldBeTrue();   // exactly 20%
        CaptureRatePolicy.IsReliable(orphanLogDays: 21, loggedVisitDays: 79).ShouldBeFalse();  // just over
    }
}
