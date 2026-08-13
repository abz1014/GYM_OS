using GymOS.Domain.Members;
using Shouldly;

namespace GymOS.Domain.Tests.Members;

/// <summary>
/// A freeze allowance belongs to the MEMBERSHIP, not to the request; a window must describe a pause
/// that can actually still happen; and a resume may only give back time the member actually lost.
///
/// THE DEFECT THESE PIN. MaxFreezeDays was evaluated against a single request with no memory of what
/// had been spent, and resuming credited the whole requested window regardless of how much of it had
/// elapsed. So freezing a window that had not started yet and immediately resuming banked its full
/// length, repeatably. Reproduced on a live database: three cycles of the identical 30-day window
/// moved a paid-up-to date from 2027-07-21 to 2027-10-19. Ninety days of membership, from nothing,
/// on a $449.99 plan, with nothing stopping a fourth cycle.
///
/// An adversarial pass on the first version of the fix then found the same minting shape hiding in
/// window PLACEMENT: a window dated entirely in the past (live repro: 2020-01-01..31, frozen and
/// resumed in seconds) credited its full length for time the member spent training. Hence the two
/// placement rules: a freeze cannot start before the membership does, and cannot already be over.
/// </summary>
public class MembershipFreezePolicyTests
{
    private static readonly DateOnly MembershipStart = new(2025, 6, 1);
    private static readonly DateOnly Today = new(2026, 1, 1);
    private static readonly DateOnly Jan1 = Today;

    private static (bool Allowed, string? Reason) Evaluate(
        int maxFreezeDays, int daysAlreadyUsed, DateOnly freezeStart, DateOnly freezeEnd) =>
        MembershipFreezePolicy.Evaluate(maxFreezeDays, daysAlreadyUsed, MembershipStart, Today, freezeStart, freezeEnd);

    // ---- the allowance is cumulative ----

    [Fact]
    public void A_freeze_within_an_untouched_allowance_is_allowed()
    {
        var (allowed, reason) = Evaluate(30, daysAlreadyUsed: 0, Jan1, Jan1.AddDays(30));

        allowed.ShouldBeTrue();
        reason.ShouldBeNull();
    }

    [Fact]
    public void Days_already_spent_come_off_the_allowance()
    {
        // The heart of it: 30 allowed, 25 already used, so a 10-day request must fail even though 10
        // is comfortably under 30. This is the check that did not exist.
        var (allowed, reason) = Evaluate(30, daysAlreadyUsed: 25, Jan1, Jan1.AddDays(10));

        allowed.ShouldBeFalse();
        reason.ShouldBe("This membership has 5 of its 30 freeze day(s) left; 10 requested.");
    }

    [Fact]
    public void The_remainder_of_an_allowance_is_still_usable()
    {
        var (allowed, _) = Evaluate(30, daysAlreadyUsed: 25, Jan1, Jan1.AddDays(5));

        allowed.ShouldBeTrue();
    }

    [Fact]
    public void A_fully_spent_allowance_refuses_everything_further()
    {
        var (allowed, reason) = Evaluate(30, daysAlreadyUsed: 30, Jan1, Jan1.AddDays(1));

        allowed.ShouldBeFalse();
        reason.ShouldBe("This membership has 0 of its 30 freeze day(s) left; 1 requested.");
    }

    [Fact]
    public void An_over_request_on_a_fresh_membership_still_blames_the_plan_not_the_member()
    {
        // Two different conversations: "your plan never allowed this much" and "you have used yours".
        // Staff repeat these to the member, so they must not be collapsed into one sentence.
        var (_, reason) = Evaluate(7, daysAlreadyUsed: 0, Jan1, Jan1.AddDays(14));

        reason.ShouldBe("This plan allows at most 7 freeze day(s); 14 requested.");
    }

    [Fact]
    public void A_plan_with_no_allowance_still_refuses_before_anything_else()
    {
        var (allowed, reason) = Evaluate(
            MembershipFreezePolicy.NoFreezeAllowance, daysAlreadyUsed: 0, Jan1, Jan1.AddDays(1));

        allowed.ShouldBeFalse();
        reason.ShouldBe("This plan does not allow freezing.");
    }

    // ---- the window must describe a pause that can still happen ----

    [Fact]
    public void An_inverted_window_is_refused_before_the_allowance_is_considered()
    {
        var (allowed, reason) = Evaluate(30, daysAlreadyUsed: 0, Jan1.AddDays(5), Jan1);

        allowed.ShouldBeFalse();
        reason.ShouldBe("The freeze end date is before its start date.");
    }

    [Fact]
    public void A_zero_day_window_is_refused_even_when_the_allowance_would_cover_it()
    {
        /*
         * Found adversarially: 0 requested days never exceeds any remainder — including a remainder
         * of 0 — so a same-day window flipped the membership (and the member) into Frozen even after
         * the allowance was fully spent. It pauses nothing and credits nothing; refuse it as a
         * request error rather than letting it toggle status for free.
         */
        var (allowed, reason) = Evaluate(30, daysAlreadyUsed: 30, Jan1, Jan1);

        allowed.ShouldBeFalse();
        reason.ShouldBe("A freeze must last at least one day.");
    }

    [Fact]
    public void A_window_starting_before_the_membership_is_refused()
    {
        // Live repro on the first version of this fix: a window dated 2020, on a membership created
        // in 2026, was accepted — and resume then credited its full length. A membership cannot have
        // been paused before it existed.
        var (allowed, reason) = Evaluate(30, daysAlreadyUsed: 0, MembershipStart.AddDays(-10), Jan1.AddDays(5));

        allowed.ShouldBeFalse();
        reason.ShouldBe("The freeze starts before this membership does.");
    }

    [Fact]
    public void A_window_already_entirely_in_the_past_is_refused()
    {
        // The other half of the backdating hole: a window that is already over pauses nothing going
        // forward — approving it is purely a retroactive credit, which is the minting shape again.
        var (allowed, reason) = Evaluate(30, daysAlreadyUsed: 0, Today.AddDays(-20), Today.AddDays(-1));

        allowed.ShouldBeFalse();
        reason.ShouldBe("This freeze is already over — the window must reach today or later.");
    }

    [Fact]
    public void A_window_that_started_in_the_past_but_is_still_open_remains_legal()
    {
        // "She has been away since the 8th, freeze her from then" is the desk's most common freeze.
        // The backdating rules must kill the all-past window without killing this.
        var (allowed, _) = Evaluate(30, daysAlreadyUsed: 0, Today.AddDays(-5), Today.AddDays(9));

        allowed.ShouldBeTrue();
    }

    [Fact]
    public void A_window_ending_today_is_still_a_window()
    {
        var (allowed, _) = Evaluate(30, daysAlreadyUsed: 0, Today.AddDays(-14), Today);

        allowed.ShouldBeTrue();
    }

    // ---- a resume only gives back time that was actually lost ----

    [Fact]
    public void A_freeze_that_never_started_credits_nothing()
    {
        /*
         * The exploit in one line. Freeze a window a month out, resume immediately, and the old code
         * handed over the whole window — for a pause that had not begun and cost the member nothing.
         */
        MembershipFreezePolicy.CreditableDays(Jan1.AddDays(30), Jan1.AddDays(60), resumedOn: Jan1)
            .ShouldBe(0);
    }

    [Fact]
    public void Resuming_early_credits_only_the_days_already_frozen()
    {
        // Frozen on the 1st for 30 days, resumed on the 10th: nine days were paused, twenty-one were
        // never taken and must not be paid for.
        MembershipFreezePolicy.CreditableDays(Jan1, Jan1.AddDays(30), resumedOn: Jan1.AddDays(9))
            .ShouldBe(9);
    }

    [Fact]
    public void Resuming_after_the_window_credits_the_window_and_no_more()
    {
        // A member who forgets to resume does not keep accruing credit past the end they asked for.
        MembershipFreezePolicy.CreditableDays(Jan1, Jan1.AddDays(30), resumedOn: Jan1.AddDays(90))
            .ShouldBe(30);
    }

    [Fact]
    public void Resuming_on_the_last_day_credits_the_whole_window()
    {
        MembershipFreezePolicy.CreditableDays(Jan1, Jan1.AddDays(30), resumedOn: Jan1.AddDays(30))
            .ShouldBe(30);
    }

    [Fact]
    public void An_inverted_window_credits_nothing_rather_than_a_negative()
    {
        // Defence in depth: the validator rejects this shape, but a credit calculator that can return
        // a negative would SHORTEN a membership somebody paid for.
        MembershipFreezePolicy.CreditableDays(Jan1.AddDays(5), Jan1, resumedOn: Jan1.AddDays(10))
            .ShouldBe(0);
    }

    [Fact]
    public void Repeating_the_same_window_can_never_out_earn_the_allowance()
    {
        /*
         * The end-to-end statement of the original bug, at policy level: replay the same freeze five
         * times, crediting and counting each one honestly, and the total credited can never exceed
         * what the plan allows — because each cycle's credit is charged against the allowance that
         * gates the next one. Previously this loop was unbounded.
         */
        var allowance = 30;
        var used = 0;
        var totalCredited = 0;

        for (var cycle = 0; cycle < 5; cycle++)
        {
            var start = Jan1;
            var end = Jan1.AddDays(14);
            var (allowed, _) = Evaluate(allowance, used, start, end);
            if (!allowed)
            {
                continue;
            }

            // Taken in full, every time — the most generous reading of each cycle.
            var credited = MembershipFreezePolicy.CreditableDays(start, end, end);
            totalCredited += credited;
            used += credited;
        }

        used.ShouldBeLessThanOrEqualTo(allowance);
        // Two cycles of 14 are honoured; the remaining three are refused with 2 days left, which is
        // the whole point — the loop terminates against the allowance instead of running forever.
        totalCredited.ShouldBe(28);
    }
}
