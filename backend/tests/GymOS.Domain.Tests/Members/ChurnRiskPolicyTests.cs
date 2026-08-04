using GymOS.Domain.Members;
using Shouldly;

namespace GymOS.Domain.Tests.Members;

/// <summary>
/// Who gets the automated "we miss you" message. The failure modes here are commercial, not
/// technical — messaging a member who never joined properly, or nagging the same person weekly,
/// both make the gym look careless — so each guard is pinned by its own test.
/// </summary>
public class ChurnRiskPolicyTests
{
    private static readonly DateOnly Today = new(2026, 8, 4);

    [Fact]
    public void Flags_an_active_member_who_has_stopped_coming()
    {
        var lastSeen = Today.AddDays(-ChurnRiskPolicy.InactivityThresholdDays);

        ChurnRiskPolicy.ShouldSendWinBack(MemberStatus.Active, lastSeen, null, Today).ShouldBeTrue();
    }

    [Fact]
    public void Leaves_alone_a_member_who_visited_recently()
    {
        var lastSeen = Today.AddDays(-(ChurnRiskPolicy.InactivityThresholdDays - 1));

        ChurnRiskPolicy.ShouldSendWinBack(MemberStatus.Active, lastSeen, null, Today).ShouldBeFalse();
    }

    [Fact]
    public void Never_chases_a_member_who_has_never_visited()
    {
        // A member who signed up but never came is an onboarding problem — "we miss you" would be wrong.
        ChurnRiskPolicy.ShouldSendWinBack(MemberStatus.Active, null, null, Today).ShouldBeFalse();
    }

    [Theory]
    [InlineData(MemberStatus.Frozen)]
    [InlineData(MemberStatus.Expired)]
    [InlineData(MemberStatus.Cancelled)]
    public void Only_chases_members_who_are_still_paying(MemberStatus status)
    {
        var lastSeen = Today.AddDays(-60);

        ChurnRiskPolicy.ShouldSendWinBack(status, lastSeen, null, Today).ShouldBeFalse();
    }

    [Fact]
    public void Does_not_re_nag_inside_the_cooldown()
    {
        var lastSeen = Today.AddDays(-60);
        var chasedRecently = Today.AddDays(-(ChurnRiskPolicy.ResendCooldownDays - 1));

        ChurnRiskPolicy.ShouldSendWinBack(MemberStatus.Active, lastSeen, chasedRecently, Today).ShouldBeFalse();
    }

    [Fact]
    public void Chases_again_once_the_cooldown_has_passed()
    {
        var lastSeen = Today.AddDays(-60);
        var chasedLongAgo = Today.AddDays(-ChurnRiskPolicy.ResendCooldownDays);

        ChurnRiskPolicy.ShouldSendWinBack(MemberStatus.Active, lastSeen, chasedLongAgo, Today).ShouldBeTrue();
    }
}
