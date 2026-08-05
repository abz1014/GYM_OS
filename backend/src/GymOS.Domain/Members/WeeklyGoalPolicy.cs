namespace GymOS.Domain.Members;

/// <summary>
/// The rules behind the member home screen's weekly ring: what counts as a session, which days are
/// "this week", and what a legal goal is.
///
/// Pure, and deliberately server-side. The browser used to derive all of this itself, which put the
/// rule in two places with nothing keeping them honest — and they had already drifted: the client
/// counted a day as trained only when its lifting VOLUME was above zero, so a bodyweight or cardio
/// session (no weight recorded, volume therefore 0) left the ring empty for a member who had
/// genuinely trained. Sessions are counted here, once, from the workout log itself.
/// </summary>
public static class WeeklyGoalPolicy
{
    /// <summary>Sessions a week for a member who has never set a goal. Three is the standard
    /// "consistent trainee" baseline — enough to be a real habit, low enough to stay reachable.</summary>
    public const int DefaultWeeklySessionGoal = 3;

    public const int MinWeeklySessionGoal = 1;

    /// <summary>Two a day, every day. Not a training recommendation — just the point beyond which a
    /// number is a typo or an attempt to make the ring meaningless, rather than a plan.</summary>
    public const int MaxWeeklySessionGoal = 14;

    public static bool IsValidGoal(int goal) => goal is >= MinWeeklySessionGoal and <= MaxWeeklySessionGoal;

    /// <summary>
    /// Distinct days trained within the Monday-start week containing <paramref name="today"/>.
    ///
    /// Counted per DAY rather than per logged workout: a member who logs their lifting and their
    /// cardio as two entries trained once that day, and shouldn't close the ring twice as fast as
    /// someone who logged one combined session. The week boundary comes from
    /// <see cref="StreakCalculator.WeekStart"/> so the ring and the streak flame — which sit side by
    /// side on the home screen — can never disagree about where the week starts.
    /// </summary>
    public static int SessionsThisWeek(IEnumerable<DateOnly> sessionDates, DateOnly today)
    {
        var weekStart = StreakCalculator.WeekStart(today);
        var weekEnd = weekStart.AddDays(6);

        return sessionDates.Where(d => d >= weekStart && d <= weekEnd).Distinct().Count();
    }

    /// <summary>Sessions still needed this week. Zero once the goal is met — never negative, because
    /// "-1 more sessions to go" is not a thing anyone wants to read.</summary>
    public static int RemainingSessions(int sessionsThisWeek, int goal) => Math.Max(0, goal - sessionsThisWeek);

    public static bool IsGoalMet(int sessionsThisWeek, int goal) => sessionsThisWeek >= goal;
}
