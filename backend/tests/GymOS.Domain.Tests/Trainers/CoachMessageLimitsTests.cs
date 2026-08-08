using GymOS.Domain.Trainers;
using Shouldly;

namespace GymOS.Domain.Tests.Trainers;

/// <summary>
/// The two bounds on a coaching conversation: how fast one side may write into it, and how long any
/// of it is kept. Both are arithmetic; what is pinned here is the edges, because both rules fail
/// quietly — a rate limit that is off by one silences somebody mid-sentence, and a retention rule
/// that is off by one deletes correspondence a day early.
/// </summary>
public class CoachMessageLimitsTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_normal_back_and_forth_is_never_interrupted()
    {
        // Ten messages in ten minutes: a member describing a niggle and working through it. If the
        // limit ever bites here it is the wrong limit.
        var burst = Enumerable.Range(1, 10).Select(i => Now.AddMinutes(-i)).ToList();

        CoachMessagePolicy.IsWithinRateLimit(burst, Now).ShouldBeTrue();
    }

    [Fact]
    public void The_allowance_is_exhausted_only_once_it_is_actually_full()
    {
        var oneShort = Enumerable.Range(1, CoachMessagePolicy.MaxMessagesPerHour - 1)
            .Select(i => Now.AddMinutes(-i)).ToList();
        var full = Enumerable.Range(1, CoachMessagePolicy.MaxMessagesPerHour)
            .Select(i => Now.AddMinutes(-i)).ToList();

        CoachMessagePolicy.IsWithinRateLimit(oneShort, Now).ShouldBeTrue();
        CoachMessagePolicy.IsWithinRateLimit(full, Now).ShouldBeFalse();
    }

    [Fact]
    public void The_window_rolls_rather_than_resetting_on_the_hour()
    {
        // The full allowance, sent just over an hour ago. A fixed clock hour would let somebody send
        // twice the allowance a minute apart across the boundary; a rolling window does not.
        var spent = Enumerable.Range(0, CoachMessagePolicy.MaxMessagesPerHour)
            .Select(i => Now.AddMinutes(-61 - i)).ToList();

        CoachMessagePolicy.IsWithinRateLimit(spent, Now).ShouldBeTrue();
    }

    [Fact]
    public void Messages_older_than_the_retention_period_are_expired()
    {
        var justInside = Now - CoachMessagePolicy.RetentionPeriod + TimeSpan.FromDays(1);
        var justOutside = Now - CoachMessagePolicy.RetentionPeriod - TimeSpan.FromDays(1);

        CoachMessagePolicy.IsExpired(justInside, Now).ShouldBeFalse();
        CoachMessagePolicy.IsExpired(justOutside, Now).ShouldBeTrue();
    }

    [Fact]
    public void Retention_is_two_years_and_saying_so_out_loud_is_the_point()
    {
        // Pinned because it is a policy decision rather than an implementation detail: if somebody
        // shortens it, that should be a deliberate edit to a failing test, not a quiet constant bump.
        CoachMessagePolicy.RetentionPeriod.TotalDays.ShouldBe(730);
    }
}
