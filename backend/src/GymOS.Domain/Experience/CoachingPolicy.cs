using GymOS.Domain.Members;

namespace GymOS.Domain.Experience;

/// <summary>
/// Pure rules behind the trainer/manager coaching dashboards (blueprint Phase 13). Plateau detection
/// and overtraining risk already exist as ProgressiveOverloadPolicy and RecoveryPolicy — this adds the
/// two things those don't cover: adherence-as-a-percentage (so a trainer can scan many members at a
/// glance) and "is this member's streak about to lapse." Deterministic thresholds only, same as every
/// other MEE policy — no AI, no per-tenant tuning.
/// </summary>
public static class CoachingPolicy
{
    /// <summary>A member's nutrition-tracking signal: whether they've ever had a diet plan at all
    /// (distinguishes "never assigned" from "assigned but not logging"), the dates within the trailing
    /// window they had an active plan, and the dates within that same window they actually logged a
    /// consumed meal.</summary>
    /// <param name="AdherenceConfirmedDatesInWindow">Days the member tapped "stayed on plan".
    /// Unioned with the meal dates rather than replacing them — see NutritionAdherencePercent.</param>
    public readonly record struct NutritionAdherenceSignals(
        bool HasEverHadPlan,
        IReadOnlyList<DateOnly> PlanActiveDatesInWindow,
        IReadOnlyList<DateOnly> MealLoggedDatesInWindow,
        IReadOnlyList<DateOnly> AdherenceConfirmedDatesInWindow);

    /// <summary>
    /// Consistency over the trailing N calendar weeks (this week and the N-1 before it), as a
    /// percentage: weeks with at least one logged workout divided by the window size. A percentage
    /// scans better than a raw streak count across a roster of many members, and — unlike a streak —
    /// doesn't reset to zero on the very first missed week, which would swamp a trainer's list with
    /// members who are still mostly on track.
    /// </summary>
    public static int WorkoutAdherencePercent(IReadOnlyList<DateOnly> workoutDates, DateOnly today, int windowWeeks = 4)
    {
        var weeksWithActivity = new HashSet<DateOnly>();
        for (var offset = 0; offset < windowWeeks; offset++)
        {
            var weekStart = StreakCalculator.WeekStart(today).AddDays(-7 * offset);
            var weekEnd = weekStart.AddDays(6);
            if (workoutDates.Any(d => d >= weekStart && d <= weekEnd))
            {
                weeksWithActivity.Add(weekStart);
            }
        }

        return (int)Math.Round(weeksWithActivity.Count * 100m / windowWeeks);
    }

    /// <summary>
    /// Of the days a member actually had an active diet plan within the trailing window, what
    /// fraction did they log a meal on. Null — not zero — when the member has never had a diet plan
    /// at all, or had one but none of its active days fall in the window: "no data" reads differently
    /// to a trainer than "assigned and ignoring it," the same distinction ChurnRiskPolicy draws for
    /// members who've never checked in.
    /// </summary>
    public static int? NutritionAdherencePercent(NutritionAdherenceSignals signals)
    {
        if (!signals.HasEverHadPlan || signals.PlanActiveDatesInWindow.Count == 0)
        {
            return null;
        }

        /*
         * A day counts if the member said so OR logged a meal — the union, not one or the other.
         *
         * Adherence is a self-report and always was; counting meal rows was a proxy for it, made
         * necessary because a tick did not exist. Now that it does, this measures the thing it is
         * named after directly. The meal dates STAY in the union for two reasons: a member who
         * prefers the food diary is genuinely adhering and must not be marked down for using the
         * older path, and every historic row in the database was recorded that way — dropping it
         * would rewrite every existing member's compliance downwards overnight.
         */
        var compliantDates = signals.MealLoggedDatesInWindow
            .Concat(signals.AdherenceConfirmedDatesInWindow)
            .ToHashSet();

        var compliantDays = signals.PlanActiveDatesInWindow.Count(d => compliantDates.Contains(d));
        return (int)Math.Round(compliantDays * 100m / signals.PlanActiveDatesInWindow.Count);
    }

    /// <summary>
    /// True when a member's weekly check-in streak is still alive only on the grace period —
    /// StreakCalculator.CurrentWeeklyStreak already counts a week where they simply "haven't been in
    /// YET"; this flags exactly that case as an actionable nudge before the streak actually breaks.
    /// </summary>
    public static bool IsStreakBreakImminent(bool visitedThisWeek, int currentWeeklyStreak)
        => !visitedThisWeek && currentWeeklyStreak > 0;
}
