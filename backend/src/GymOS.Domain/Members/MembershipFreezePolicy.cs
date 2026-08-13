namespace GymOS.Domain.Members;

/// <summary>
/// Whether a membership may be frozen for a given span, how much of its allowance that leaves, and
/// how many days a resume is actually allowed to give back.
///
/// This exists because the same rule is applied from two places — freezing one membership from a
/// member's page, and freezing a selection from the members list — and the rule is not uniform across
/// a selection: <c>MaxFreezeDays</c> belongs to the PLAN, so freezing twenty members for fourteen days
/// is legal for the ones on Annual and illegal for the ones on a plan that allows seven. A batch
/// therefore cannot be all-or-nothing, and the caller needs the reason per member rather than a count.
///
/// Returning the reason as text rather than an enum is deliberate: it is written once here, next to
/// the number that makes it true, instead of being re-worded in each caller's error handling.
///
/// WHAT THIS USED TO GET WRONG, and why the shape changed. The allowance was checked against ONE
/// request in isolation and nothing remembered what had already been spent, while resuming credited
/// the whole requested window back onto EndDate regardless of how much of it had elapsed. A staff
/// user could therefore freeze and resume the same untouched future window over and over, and each
/// round trip added its full length to the membership. Measured on a live database: three cycles of
/// the identical 30-day window moved a paid-up-to date from 2027-07-21 to 2027-10-19 — ninety days of
/// membership created out of nothing on a $449.99 plan, with no upper bound on repetition.
///
/// The two halves of the fix live here together on purpose: the ceiling is only meaningful if
/// something counts against it, and the count is only honest if a resume can never give back time
/// that was not actually frozen.
/// </summary>
public static class MembershipFreezePolicy
{
    /// <summary>
    /// A plan with no freeze allowance at all. Distinguished from "asked for too many days" because
    /// it is a different conversation with the member: one is negotiable, the other is the plan.
    /// </summary>
    public const int NoFreezeAllowance = 0;

    /// <summary>
    /// Whether this freeze may go ahead, given what the plan allows, what the membership has
    /// already spent, and whether the window describes a pause that can actually still happen.
    /// </summary>
    /// <param name="daysAlreadyUsed">Freeze days this membership has already consumed and been
    /// credited for. The parameter that did not exist, and whose absence made the allowance
    /// per-request rather than per-membership.</param>
    /// <param name="membershipStartDate">A membership cannot have been paused before it existed —
    /// without this floor, a window dated years into the past was accepted and then credited in
    /// full on resume, for time the member spent training as a normal active member.</param>
    /// <param name="today">Used to refuse windows that are already entirely over. A freeze whose
    /// end has passed pauses nothing — it is purely a retroactive credit, which is the minting
    /// shape again. A window that STARTS in the past but is still open remains legal on purpose:
    /// "she has been away since the 8th, freeze her from then" is the desk's most common request.</param>
    public static (bool Allowed, string? Reason) Evaluate(
        int maxFreezeDays, int daysAlreadyUsed, DateOnly membershipStartDate, DateOnly today,
        DateOnly freezeStart, DateOnly freezeEnd)
    {
        if (freezeEnd < freezeStart)
        {
            return (false, "The freeze end date is before its start date.");
        }

        if (freezeEnd == freezeStart)
        {
            // Zero days pauses nothing, credits nothing, and yet used to flip the membership (and
            // the member) into Frozen — even when the allowance was fully spent, because 0 never
            // exceeds any remainder. A freeze that does not freeze is a request error.
            return (false, "A freeze must last at least one day.");
        }

        if (freezeStart < membershipStartDate)
        {
            return (false, "The freeze starts before this membership does.");
        }

        if (freezeEnd < today)
        {
            return (false, "This freeze is already over — the window must reach today or later.");
        }

        if (maxFreezeDays <= NoFreezeAllowance)
        {
            return (false, "This plan does not allow freezing.");
        }

        var requestedDays = freezeEnd.DayNumber - freezeStart.DayNumber;
        var remaining = Math.Max(0, maxFreezeDays - Math.Max(0, daysAlreadyUsed));

        if (requestedDays > remaining)
        {
            // Named separately from the plain over-request case: "you have used yours" and "your plan
            // never allowed this much" are different answers, and staff repeat them to the member.
            return daysAlreadyUsed > 0
                ? (false, $"This membership has {remaining} of its {maxFreezeDays} freeze day(s) left; {requestedDays} requested.")
                : (false, $"This plan allows at most {maxFreezeDays} freeze day(s); {requestedDays} requested.");
        }

        return (true, null);
    }

    /// <summary>
    /// How many days a resume may add back to <c>EndDate</c> — the time the membership was ACTUALLY
    /// paused, never the length that was originally asked for.
    ///
    /// Resuming used to credit the whole requested window, so freezing a window that had not started
    /// yet and immediately resuming banked its full length for nothing. The frozen period really runs
    /// from the freeze start until whichever comes first: the day the member resumed, or the freeze
    /// end. Everything else is time the member spent as a normal, active, using-the-gym member.
    /// </summary>
    public static int CreditableDays(DateOnly freezeStart, DateOnly freezeEnd, DateOnly resumedOn)
    {
        if (freezeEnd < freezeStart)
        {
            return 0;
        }

        var effectiveEnd = resumedOn < freezeEnd ? resumedOn : freezeEnd;

        // Negative when the freeze had not begun — clamped, because a freeze that never started
        // cannot have cost the member anything to give back.
        return Math.Max(0, effectiveEnd.DayNumber - freezeStart.DayNumber);
    }
}
