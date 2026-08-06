using GymOS.Domain.Trainers;
using Shouldly;

namespace GymOS.Domain.Tests.Trainers;

/// <summary>
/// Who may correspond with whom. The rule under test is that messaging follows an active pairing —
/// the boundary that stops "message my trainer" becoming a way to reach any trainer in the gym.
///
/// Today is Thursday 2026-08-06.
/// </summary>
public class CoachMessagePolicyTests
{
    private static readonly DateOnly Today = new(2026, 8, 6);

    [Fact]
    public void An_active_pairing_may_correspond()
    {
        CoachMessagePolicy.CanSend(Today.AddDays(-30), null, isActive: true, Today).ShouldBeTrue();
    }

    [Fact]
    public void Someone_with_no_trainer_at_all_cannot_send()
    {
        CoachMessagePolicy.CanSend(null, null, isActive: false, Today).ShouldBeFalse();
    }

    [Fact]
    public void A_pairing_that_has_ended_can_be_read_but_not_added_to()
    {
        // The history stays; a member should still be able to read what their old coach told them.
        CoachMessagePolicy.CanSend(Today.AddDays(-90), Today.AddDays(-1), isActive: true, Today).ShouldBeFalse();
    }

    [Fact]
    public void A_pairing_that_has_not_started_cannot_send_yet()
    {
        CoachMessagePolicy.CanSend(Today.AddDays(1), null, isActive: true, Today).ShouldBeFalse();
    }

    [Fact]
    public void A_deactivated_pairing_cannot_send_even_inside_its_dates()
    {
        CoachMessagePolicy.CanSend(Today.AddDays(-30), Today.AddDays(30), isActive: false, Today).ShouldBeFalse();
    }

    [Fact]
    public void The_first_and_last_day_of_a_pairing_both_count()
    {
        CoachMessagePolicy.CanSend(Today, Today, isActive: true, Today).ShouldBeTrue();
    }

    [Fact]
    public void An_empty_message_is_not_a_message()
    {
        CoachMessagePolicy.IsSendable(null).ShouldBeFalse();
        CoachMessagePolicy.IsSendable("").ShouldBeFalse();
        CoachMessagePolicy.IsSendable("   \n  ").ShouldBeFalse();
    }

    [Fact]
    public void A_message_longer_than_the_bound_is_rejected_on_its_trimmed_length()
    {
        // Padding should not be what pushes someone over the limit.
        CoachMessagePolicy.IsSendable(new string('a', CoachMessagePolicy.MaxBodyLength)).ShouldBeTrue();
        CoachMessagePolicy.IsSendable(new string('a', CoachMessagePolicy.MaxBodyLength + 1)).ShouldBeFalse();
        CoachMessagePolicy.IsSendable("  " + new string('a', CoachMessagePolicy.MaxBodyLength) + "  ").ShouldBeTrue();
    }

    [Fact]
    public void Stored_text_loses_its_surrounding_whitespace()
    {
        CoachMessagePolicy.Normalise("  Nice squat session.\n ").ShouldBe("Nice squat session.");
    }

    [Fact]
    public void Only_the_other_sides_messages_count_as_unread()
    {
        (CoachMessageAuthor, DateTimeOffset?)[] thread =
        [
            (CoachMessageAuthor.Trainer, null),                  // unread by the member
            (CoachMessageAuthor.Trainer, DateTimeOffset.UnixEpoch),
            (CoachMessageAuthor.Member, null),                   // the member's own — not news to them
        ];

        CoachMessagePolicy.UnreadFor(CoachMessageAuthor.Member, thread).ShouldBe(1);
        CoachMessagePolicy.UnreadFor(CoachMessageAuthor.Trainer, thread).ShouldBe(1);
    }

    [Fact]
    public void An_empty_conversation_carries_no_badge()
    {
        CoachMessagePolicy.UnreadFor(CoachMessageAuthor.Member, []).ShouldBe(0);
    }
}
