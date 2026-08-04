namespace GymOS.Domain.Members;

/// <summary>
/// Turns a member's check-in history into a weekly streak — "N consecutive weeks with at least one
/// visit". Weekly rather than daily on purpose: a 3-visits-a-week gym-goer shouldn't lose their
/// streak over a rest day, and weekly streaks are the convention members already know from other
/// fitness apps. Pure (dates in, number out) so the rule is unit-testable and shared by the portal
/// query and any future badge/notification logic.
/// </summary>
public static class StreakCalculator
{
    /// <summary>
    /// Consecutive calendar weeks (Monday-start) with ≥1 check-in, counting backwards from the
    /// current week. The current week counts if it has a visit; if it doesn't yet, the streak is
    /// still alive as long as LAST week had one (the member simply hasn't been in this week YET) —
    /// only a fully-missed prior week breaks it.
    /// </summary>
    public static int CurrentWeeklyStreak(IEnumerable<DateOnly> checkInDates, DateOnly today)
    {
        var visitedWeeks = checkInDates.Select(WeekStart).ToHashSet();

        if (visitedWeeks.Count == 0)
        {
            return 0;
        }

        var thisWeek = WeekStart(today);

        // Anchor: this week if visited, else last week (grace for a week in progress), else broken.
        var anchor = visitedWeeks.Contains(thisWeek) ? thisWeek
            : visitedWeeks.Contains(thisWeek.AddDays(-7)) ? thisWeek.AddDays(-7)
            : (DateOnly?)null;

        if (anchor is null)
        {
            return 0;
        }

        var streak = 0;
        for (var week = anchor.Value; visitedWeeks.Contains(week); week = week.AddDays(-7))
        {
            streak++;
        }

        return streak;
    }

    /// <summary>Monday of the week containing <paramref name="date"/>.</summary>
    public static DateOnly WeekStart(DateOnly date)
    {
        // DayOfWeek: Sunday=0 … Saturday=6; map to days-since-Monday.
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-daysSinceMonday);
    }
}
