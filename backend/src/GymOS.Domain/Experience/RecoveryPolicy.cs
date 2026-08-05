namespace GymOS.Domain.Experience;

/// <summary>
/// The pure recovery heuristic. Given a member's recent training signals — how many days they trained
/// and rested in the trailing week, and how long since the last session — it classifies overall (and
/// per-muscle-group) <see cref="RecoveryStatus"/> and returns a plain-language reason. No I/O and no
/// dates: the query pre-reduces history to counts, so the rule is identical in an endpoint, a rebuild,
/// or a unit test. Deterministic thresholds only — "no wearable required" (blueprint Phase 9).
///
/// Logged rest days genuinely move the needle: at four sessions a week, logging a recovery day pulls a
/// member from <see cref="RecoveryStatus.Fatigued"/> back to <see cref="RecoveryStatus.Ready"/> — so
/// the XP reward for logging recovery and the recovery status reinforce the same healthy habit.
/// </summary>
public static class RecoveryPolicy
{
    /// <summary>A member's whole-body training signals over a trailing 7-day window. DaysSinceLastSession
    /// is null when they have never logged a workout.</summary>
    public readonly record struct RecoverySignals(int SessionsLast7Days, int RestDaysLast7Days, int? DaysSinceLastSession);

    /// <summary>One muscle group's training signals over a trailing 7-day window. DaysSinceLastTrained
    /// is null when the group has no history.</summary>
    public readonly record struct MuscleRecoverySignals(string MuscleGroup, int TimesLast7Days, int? DaysSinceLastTrained);

    public static (RecoveryStatus Status, string Reason) ClassifyOverall(RecoverySignals s)
    {
        if (s.DaysSinceLastSession is null)
        {
            return (RecoveryStatus.Fresh,
                "No recent workouts logged — you're fully rested. Log a session to get your momentum going.");
        }

        // Three or more days off reads as fully recovered regardless of last week's volume.
        if (s.DaysSinceLastSession >= 3)
        {
            var days = s.DaysSinceLastSession.Value;
            return (RecoveryStatus.Fresh,
                $"Your last workout was {days} day{(days == 1 ? "" : "s")} ago — you're fully recovered and ready for a strong session.");
        }

        // Near-daily training with no rest logged all week is the clearest overtraining signal.
        if (s.SessionsLast7Days >= 6 && s.RestDaysLast7Days == 0)
        {
            return (RecoveryStatus.OvertrainingRisk,
                $"{s.SessionsLast7Days} sessions in the last 7 days with no rest logged — take a recovery day to avoid overtraining.");
        }

        // High load, or a moderately high load with no rest, reads as fatigued.
        if (s.SessionsLast7Days >= 5 || (s.SessionsLast7Days >= 4 && s.RestDaysLast7Days == 0))
        {
            return (RecoveryStatus.Fatigued,
                $"You've trained {s.SessionsLast7Days} of the last 7 days — consider a lighter session or a rest day.");
        }

        return (RecoveryStatus.Ready,
            "Your training load is well balanced — you're recovered and ready to go.");
    }

    public static (RecoveryStatus Status, string Reason) ClassifyMuscleGroup(MuscleRecoverySignals s)
    {
        // Hit four or more times in a week — the group needs a break before the next session.
        if (s.TimesLast7Days >= 4)
        {
            return (RecoveryStatus.OvertrainingRisk,
                $"Trained {s.TimesLast7Days}× in the last 7 days — give it 48 hours before hitting it again.");
        }

        // No recent work — fully rested and a good target next.
        if (s.DaysSinceLastTrained is null || s.DaysSinceLastTrained >= 4)
        {
            return (RecoveryStatus.Fresh, "Fully rested — a good target for your next session.");
        }

        // Trained today or yesterday — still inside the 48-hour recovery window.
        if (s.DaysSinceLastTrained <= 1)
        {
            return (RecoveryStatus.Fatigued, "Trained in the last day — still recovering, so train something else.");
        }

        return (RecoveryStatus.Ready, "Recovered and ready to train.");
    }
}
