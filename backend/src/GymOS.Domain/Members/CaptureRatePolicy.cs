namespace GymOS.Domain.Members;

/// <summary>A single period's capture figures.</summary>
/// <param name="LoggedVisitDays">Visit days that also carry a logged workout.</param>
/// <param name="OrphanLogDays">Days a workout was logged with no visit on record. A data-integrity
/// signal rather than a performance one — see <see cref="CaptureRatePolicy"/>.</param>
public record CapturePoint(DateOnly PeriodStart, int VisitDays, int LoggedVisitDays, int OrphanLogDays)
{
    public int CaptureRatePercent => CaptureRatePolicy.RatePercent(LoggedVisitDays, VisitDays);
}

/// <summary>
/// Capture rate: the share of gym visits that produced a recorded workout.
///
/// This is the number the member-experience work is judged on. Everything the engine gives a member
/// back — XP, records, mastery, streaks, leaderboards — can only be computed from sessions that were
/// actually recorded, so a gym where people train hard and log nothing has an engine running on air.
/// Measuring it needs no new data: attendance says someone came, workout logs say something was
/// recorded, and both already exist for any gym using GymOS.
///
/// Counted in DAYS on both sides, matching <see cref="WeeklyGoalPolicy.SessionsThisWeek"/>. Two
/// check-ins in one day is one visit; five logged entries on one day is one recorded session. Rates
/// built from raw row counts would move whenever someone swiped twice or split a workout in two.
/// </summary>
public static class CaptureRatePolicy
{
    /// <summary>
    /// Whole-percent capture rate, rounded half-up. Zero visits reports 0 rather than dividing by
    /// zero — a gym with no attendance has no capture problem to report, only an attendance one.
    /// </summary>
    public static int RatePercent(int loggedVisitDays, int visitDays)
        => visitDays <= 0 ? 0 : (int)Math.Round(loggedVisitDays * 100.0 / visitDays, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Whether the reported rate should be trusted.
    ///
    /// Capture rate is trivially gameable: make confirmation frictionless enough and it climbs while
    /// recording workouts that never happened. Sessions logged on days with no visit are the tell —
    /// a few are normal (training at home, a staff correction, a member who forgot to scan in), but a
    /// meaningful share means the rate is measuring something other than what it claims to.
    /// </summary>
    public static bool IsReliable(int orphanLogDays, int loggedVisitDays)
    {
        var total = orphanLogDays + loggedVisitDays;
        return total == 0 || orphanLogDays * 100.0 / total <= MaxTrustedOrphanPercent;
    }

    /// <summary>Above this share of off-site logs, the rate stops describing gym behaviour.</summary>
    public const int MaxTrustedOrphanPercent = 20;
}
