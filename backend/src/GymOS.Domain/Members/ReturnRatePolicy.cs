namespace GymOS.Domain.Members;

/// <summary>One week-N return figure, and the cohort it was computed over.</summary>
/// <param name="WeekNumber">Weeks after joining. Week 1 is the joining week itself.</param>
/// <param name="Eligible">Members who have been members long enough for this week to have happened.</param>
/// <param name="Returned">Of those, how many visited during that week.</param>
public record ReturnRatePoint(int WeekNumber, int Eligible, int Returned)
{
    public int RatePercent => ReturnRatePolicy.RatePercent(Returned, Eligible);
}

/// <summary>
/// Week-N return rate: of the members who joined long enough ago to have reached week N, what share
/// came to the gym during that week.
///
/// This is the outcome the other three numbers are only proxies for. Capture rate and time-to-log
/// describe how well the app observes behaviour; sessions-per-week describes volume. None of them
/// pays rent. A member who returns in week 12 is the only evidence that any of it worked.
///
/// **The mistake this type exists to prevent** is counting members who have not had time to return.
/// A gym that signed thirty people last week has thirty members who cannot possibly have a week-12
/// outcome yet. Divide by them anyway and week-12 return collapses toward zero, and it collapses
/// hardest exactly when the gym is growing fastest — so the number would look worst at the moment
/// the business was doing best. Eligibility is therefore separated from outcome: a member counts in
/// the denominator only once <see cref="IsEligibleForWeek"/> says their week N is fully in the past.
/// </summary>
public static class ReturnRatePolicy
{
    /// <summary>The weeks the gates are judged on. From the roadmap's Step 0 definition.</summary>
    public static readonly IReadOnlyList<int> GateWeeks = [2, 4, 12];

    /// <summary>
    /// The date range of week N relative to a join date, inclusive. Week 1 is the joining week
    /// (days 0-6), so week N starts 7*(N-1) days after joining.
    /// </summary>
    public static (DateOnly Start, DateOnly End) WeekWindow(DateOnly joinDate, int weekNumber)
    {
        var start = joinDate.AddDays(7 * (weekNumber - 1));
        return (start, start.AddDays(6));
    }

    /// <summary>
    /// Whether this member's week N has finished. Anyone whose week N has not fully elapsed is left
    /// out of both numerator and denominator — see the type remarks. A member still inside their
    /// week N is not a member who failed to return; they are a member whose answer is not in yet.
    /// </summary>
    public static bool IsEligibleForWeek(DateOnly joinDate, DateOnly today, int weekNumber)
        => WeekWindow(joinDate, weekNumber).End < today;

    /// <summary>Whether the member visited at all during their week N.</summary>
    public static bool ReturnedInWeek(DateOnly joinDate, IEnumerable<DateOnly> visitDays, int weekNumber)
    {
        var (start, end) = WeekWindow(joinDate, weekNumber);
        return visitDays.Any(d => d >= start && d <= end);
    }

    /// <summary>
    /// Whole-percent return rate, rounded half-up. An empty cohort reports 0 alongside an Eligible
    /// of 0, which is how a caller tells "nobody came back" from "nobody could have yet" — the
    /// percentage alone cannot carry that difference, so no surface should show it without the count.
    /// </summary>
    public static int RatePercent(int returned, int eligible)
        => eligible <= 0 ? 0 : (int)Math.Round(returned * 100.0 / eligible, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Sessions per member per week: recorded sessions divided by the members who actually showed up,
    /// over the weeks in the window.
    ///
    /// This is the guard on capture rate. Capture rate is a ratio, so it can be driven up by making
    /// confirmation frictionless while total training falls — the same handful of sessions, more
    /// diligently recorded, reads as a win. Volume per member cannot be gamed that way: it only
    /// moves if people train more. The two are meant to be read together, and a rising capture rate
    /// beside a falling volume is the signal that the ratio is lying.
    ///
    /// Null when nobody visited — a gym with no attendance has no per-member figure, and 0.0 would
    /// claim its members trained zero times rather than that there were none.
    /// </summary>
    public static double? SessionsPerMemberPerWeek(int loggedSessions, int membersWhoVisited, int weeks)
    {
        if (membersWhoVisited <= 0 || weeks <= 0)
        {
            return null;
        }

        return Math.Round(loggedSessions / (double)membersWhoVisited / weeks, 2, MidpointRounding.AwayFromZero);
    }
}
