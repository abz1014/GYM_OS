namespace GymOS.Domain.Members;

/// <summary>How soon after arriving a session was recorded.</summary>
public enum LogLatencyBucket
{
    /// <summary>Recorded before leaving — the behaviour one-tap logging is trying to produce.</summary>
    WithinTheHour,

    /// <summary>Recorded later the same day. Still remembered, but no longer in the room.</summary>
    SameDay,

    /// <summary>Recorded the day after. Reconstructed from memory by now.</summary>
    NextDay,

    /// <summary>Two days or more. The set-by-set detail in these is a guess.</summary>
    Later,
}

/// <summary>
/// Time-to-log: how long after arriving at the gym a session actually got recorded.
///
/// This is the second of the four numbers the member-experience work is judged on, and it answers a
/// question capture rate cannot. Capture rate says whether a visit produced a record at all; this
/// says whether it was produced while the member could still remember the weights. A gym at 70%
/// capture where the median record lands two days later is not logging, it is recollection, and
/// every PR and mastery figure computed from it inherits that error.
///
/// **What this does not measure, and why.** The roadmap defines time-to-log as "app open → session
/// recorded" against a "under five seconds" target. That is an interaction latency, and nothing in
/// this system observes it: there is no app-open event, no client telemetry, and no field on any
/// entity that records when a member started rather than finished. Measuring it would need
/// front-end instrumentation that does not exist. What is measured here is arrival → record, from
/// AttendanceRecord.CheckInAt to WorkoutLog.LoggedAt, both of which are real. It answers the
/// question the target was reaching for — is recording happening in the moment or afterwards —
/// without inventing an event to hang a stopwatch on.
///
/// LoggedAt is set from the clock at write time by LogWorkoutCommand, so it is genuinely the moment
/// of recording rather than the moment of training. On seeded demo data it is written as check-in
/// plus 30–80 minutes, which makes the demo median a property of the seeder rather than a fact
/// about people — the same caveat that applies to capture rate.
/// </summary>
public static class TimeToLogPolicy
{
    /// <summary>Records inside this window were almost certainly made before leaving the building.</summary>
    public const int WithinTheHourMinutes = 60;

    /// <summary>
    /// Which bucket a latency falls in. Same-day and next-day are decided on calendar days rather
    /// than elapsed hours, because "did they record it before going to bed" is the behaviour in
    /// question — a 9pm session recorded at 1am is next-day by the clock and same-session by memory,
    /// and the calendar boundary is the one a member would recognise.
    /// </summary>
    public static LogLatencyBucket Bucket(DateOnly visitDay, DateOnly logDay, int minutesElapsed)
    {
        if (minutesElapsed <= WithinTheHourMinutes && visitDay == logDay)
        {
            return LogLatencyBucket.WithinTheHour;
        }

        var dayGap = logDay.DayNumber - visitDay.DayNumber;
        return dayGap switch
        {
            <= 0 => LogLatencyBucket.SameDay,
            1 => LogLatencyBucket.NextDay,
            _ => LogLatencyBucket.Later,
        };
    }

    /// <summary>
    /// Median rather than mean, because this distribution has a long right tail — one member
    /// backfilling a month of sessions would drag an average into meaninglessness while leaving the
    /// median exactly where it belongs. Null on an empty set: there is no median of nothing, and
    /// returning 0 would read as "instant".
    /// </summary>
    public static int? MedianMinutes(IReadOnlyCollection<int> latenciesInMinutes)
    {
        if (latenciesInMinutes.Count == 0)
        {
            return null;
        }

        var sorted = latenciesInMinutes.OrderBy(m => m).ToList();
        var mid = sorted.Count / 2;

        return sorted.Count % 2 == 1
            ? sorted[mid]
            // Even count: the lower of the two middles rounded with its partner, kept as whole
            // minutes because a half-minute of precision means nothing at this scale.
            : (int)Math.Round((sorted[mid - 1] + sorted[mid]) / 2.0, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Whether a (visit, log) pair can be timed at all.
    ///
    /// A log written before that day's check-in is excluded rather than counted as zero or negative.
    /// It is a real and unremarkable thing — a member logs yesterday's home workout in the morning
    /// and comes to the gym that evening — but it does not describe how long recording took after
    /// arriving, which is the only thing this metric claims to measure.
    /// </summary>
    public static bool IsMeasurable(DateTimeOffset checkInAt, DateTimeOffset loggedAt) => loggedAt >= checkInAt;
}
