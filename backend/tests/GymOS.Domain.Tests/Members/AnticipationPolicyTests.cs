using GymOS.Domain.Members;
using Shouldly;

namespace GymOS.Domain.Tests.Members;

/// <summary>
/// The one thing a member has to look forward to.
///
/// Every test here defends the same principle: anticipation must be real and near. The app is
/// allowed to say nothing, and saying nothing is the right answer far more often than inventing a
/// reason to come back — a fabricated one is discovered once and costs the trust every honest
/// number on this screen was built to earn.
///
/// Now is Thursday 2026-08-06, 12:00.
/// </summary>
public class AnticipationPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 6, 12, 0, 0, TimeSpan.Zero);

    private static AnticipationSignals Nothing => new(null, null, null, 0, 0, 0, 0);

    [Fact]
    public void A_member_with_nothing_close_is_told_nothing()
    {
        AnticipationPolicy.Next(Nothing, Now).ShouldBeNull();
    }

    [Fact]
    public void A_booked_class_is_what_they_have_coming()
    {
        var signals = Nothing with { NextClassName = "Strength Circuit", NextClassAt = Now.AddHours(6) };

        var next = AnticipationPolicy.Next(signals, Now)!.Value;

        next.Kind.ShouldBe(AnticipationKind.BookedClass);
        next.Title.ShouldBe("Strength Circuit");
        next.Detail.ShouldStartWith("Today at");
    }

    [Fact]
    public void A_booked_class_outranks_everything_else()
    {
        // It is the only item the member has already promised themselves.
        var signals = new AnticipationSignals(
            "Spin", Now.AddDays(1), "Titan Iron Challenge", 1, 7, 50, 50);

        AnticipationPolicy.Next(signals, Now)!.Value.Kind.ShouldBe(AnticipationKind.BookedClass);
    }

    [Fact]
    public void A_class_that_has_already_started_is_not_something_to_come()
    {
        var signals = Nothing with { NextClassName = "Spin", NextClassAt = Now.AddHours(-1) };

        AnticipationPolicy.Next(signals, Now).ShouldBeNull();
    }

    [Theory]
    [InlineData(1, "Tomorrow at")]
    [InlineData(3, "Sunday at")]
    [InlineData(20, "Wed 26 Aug at")]
    public void A_class_is_dated_the_way_someone_would_say_it(int daysAway, string expected)
    {
        var signals = Nothing with { NextClassName = "Yoga", NextClassAt = Now.AddDays(daysAway) };

        AnticipationPolicy.Next(signals, Now)!.Value.Detail.ShouldStartWith(expected);
    }

    [Fact]
    public void A_class_early_tomorrow_reads_as_tomorrow_not_today()
    {
        // Under 24 hours away but plainly a different day. Counting in blocks of 24 hours would say
        // "today" about a class the member will attend after they have slept.
        var signals = Nothing with { NextClassName = "Yoga", NextClassAt = Now.AddHours(20) };

        AnticipationPolicy.Next(signals, Now)!.Value.Detail.ShouldStartWith("Tomorrow at");
    }

    [Fact]
    public void A_challenge_within_reach_is_worth_saying()
    {
        var signals = Nothing with { ChallengeName = "Titan Iron Challenge", ChallengeSessionsRemaining = 3 };

        var next = AnticipationPolicy.Next(signals, Now)!.Value;

        next.Kind.ShouldBe(AnticipationKind.Challenge);
        next.Title.ShouldBe("Titan Iron Challenge");
        next.Detail.ShouldBe("3 more sessions finish it.");
    }

    [Fact]
    public void The_last_session_of_a_challenge_is_phrased_as_one()
    {
        var signals = Nothing with { ChallengeName = "Titan Iron Challenge", ChallengeSessionsRemaining = 1 };

        AnticipationPolicy.Next(signals, Now)!.Value.Detail.ShouldBe("One more session finishes it.");
    }

    [Fact]
    public void A_challenge_still_far_off_is_a_chore_not_a_prospect()
    {
        var signals = Nothing with { ChallengeName = "Titan Iron Challenge", ChallengeSessionsRemaining = 9 };

        AnticipationPolicy.Next(signals, Now).ShouldBeNull();
    }

    [Fact]
    public void A_finished_challenge_is_not_something_still_coming()
    {
        var signals = Nothing with { ChallengeName = "Titan Iron Challenge", ChallengeSessionsRemaining = 0 };

        AnticipationPolicy.Next(signals, Now).ShouldBeNull();
    }

    [Fact]
    public void A_level_within_a_few_sessions_is_counted_in_sessions_not_points()
    {
        // A member counts workouts. "150 XP to go" is a number; "about 3 more sessions" is a plan.
        var signals = Nothing with { NextLevel = 7, XpToNextLevel = 150, XpPerSession = 50 };

        var next = AnticipationPolicy.Next(signals, Now)!.Value;

        next.Kind.ShouldBe(AnticipationKind.Level);
        next.Title.ShouldBe("Level 7");
        next.Detail.ShouldBe("About 3 more sessions.");
    }

    [Fact]
    public void One_session_from_a_level_is_phrased_as_one()
    {
        var signals = Nothing with { NextLevel = 7, XpToNextLevel = 40, XpPerSession = 50 };

        AnticipationPolicy.Next(signals, Now)!.Value.Detail.ShouldBe("One more session reaches it.");
    }

    [Fact]
    public void A_level_a_long_way_off_is_left_unsaid()
    {
        var signals = Nothing with { NextLevel = 12, XpToNextLevel = 4000, XpPerSession = 50 };

        AnticipationPolicy.Next(signals, Now).ShouldBeNull();
    }

    [Fact]
    public void A_challenge_the_member_joined_outranks_a_level()
    {
        // They chose the challenge. A level happens to them.
        var signals = new AnticipationSignals(null, null, "Titan Iron Challenge", 2, 7, 40, 50);

        AnticipationPolicy.Next(signals, Now)!.Value.Kind.ShouldBe(AnticipationKind.Challenge);
    }

    [Fact]
    public void A_gym_that_awards_no_xp_per_session_does_not_divide_by_zero()
    {
        var signals = Nothing with { NextLevel = 7, XpToNextLevel = 150, XpPerSession = 0 };

        AnticipationPolicy.Next(signals, Now).ShouldBeNull();
    }
}
