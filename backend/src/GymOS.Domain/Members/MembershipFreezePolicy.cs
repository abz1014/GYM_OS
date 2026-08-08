namespace GymOS.Domain.Members;

/// <summary>
/// Whether a membership may be frozen for a given span, and if not, why in words a person can act on.
///
/// This exists because the same rule is now applied from two places — freezing one membership from a
/// member's page, and freezing a selection from the members list — and the rule is not uniform across
/// a selection: <c>MaxFreezeDays</c> belongs to the PLAN, so freezing twenty members for fourteen days
/// is legal for the ones on Annual and illegal for the ones on a plan that allows seven. A batch
/// therefore cannot be all-or-nothing, and the caller needs the reason per member rather than a count.
///
/// Returning the reason as text rather than an enum is deliberate: it is written once here, next to
/// the number that makes it true, instead of being re-worded in each caller's error handling.
/// </summary>
public static class MembershipFreezePolicy
{
    /// <summary>
    /// A plan with no freeze allowance at all. Distinguished from "asked for too many days" because
    /// it is a different conversation with the member: one is negotiable, the other is the plan.
    /// </summary>
    public const int NoFreezeAllowance = 0;

    public static (bool Allowed, string? Reason) Evaluate(int maxFreezeDays, DateOnly freezeStart, DateOnly freezeEnd)
    {
        if (freezeEnd < freezeStart)
        {
            return (false, "The freeze end date is before its start date.");
        }

        if (maxFreezeDays <= NoFreezeAllowance)
        {
            return (false, "This plan does not allow freezing.");
        }

        var requestedDays = freezeEnd.DayNumber - freezeStart.DayNumber;
        if (requestedDays > maxFreezeDays)
        {
            return (false, $"This plan allows at most {maxFreezeDays} freeze day(s); {requestedDays} requested.");
        }

        return (true, null);
    }
}
