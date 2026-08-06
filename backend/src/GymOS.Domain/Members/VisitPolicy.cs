namespace GymOS.Domain.Members;

/// <summary>Where a member stands in relation to the gym today.</summary>
public enum VisitState
{
    /// <summary>No check-in today.</summary>
    None,

    /// <summary>Checked in and not yet checked out — in the building right now.</summary>
    InGym,

    /// <summary>Came in earlier today and left.</summary>
    Visited
}

/// <summary>
/// Today's visit, and whether the training it represents was ever written down.
///
/// The turnstile already knows the member trained. Baseline capture is around a third of visits, so
/// for most of them the gym holds a check-in with nothing attached — and every streak, record and
/// weekly ring is computed from the sessions, not the visits. That gap is not a member forgetting
/// what they did; it is the app failing to ask at the one moment the answer is obvious.
///
/// So this exists to let the home screen speak from what is already known — "you were here today" —
/// rather than greeting someone who has just finished training as though they had not arrived. It
/// records nothing on its own: a visit is evidence a session happened, never proof of what was in
/// it, and inventing the contents would be exactly the dishonesty the one-tap proposal avoids.
///
/// See <see cref="CaptureRatePolicy"/>, which measures the same gap across a whole gym.
/// </summary>
public static class VisitPolicy
{
    public readonly record struct TodaysVisit(VisitState State, DateTimeOffset? CheckedInAt, bool SessionRecorded)
    {
        /// <summary>The member was here today and nothing was written down — the gap worth closing.</summary>
        public bool NeedsRecording => State != VisitState.None && !SessionRecorded;
    }

    /// <summary>
    /// Classifies today from the member's own check-ins and the dates they have sessions on.
    /// </summary>
    /// <param name="visits">Check-in/check-out pairs. Any date; only today's are considered.</param>
    /// <param name="sessionDates">Dates the member has logged a workout on.</param>
    public static TodaysVisit Classify(
        IEnumerable<(DateTimeOffset CheckInAt, DateTimeOffset? CheckOutAt)> visits,
        IEnumerable<DateOnly> sessionDates,
        DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var recorded = sessionDates.Any(d => d == today);

        // Filtering on the check-IN date is what keeps a forgotten check-out harmless. Gyms are full
        // of visits nobody ever closed; without this, one stale open record would tell a member they
        // were in the building every day forever.
        var todaysVisits = visits
            .Where(v => DateOnly.FromDateTime(v.CheckInAt.UtcDateTime) == today)
            .OrderByDescending(v => v.CheckInAt)
            .ToList();

        if (todaysVisits.Count == 0)
        {
            return new TodaysVisit(VisitState.None, null, recorded);
        }

        // An open visit wins over a closed one: someone who came back in is here now, whatever
        // earlier trip they also made today.
        var open = todaysVisits.FirstOrDefault(v => v.CheckOutAt is null);
        return open.CheckInAt != default
            ? new TodaysVisit(VisitState.InGym, open.CheckInAt, recorded)
            : new TodaysVisit(VisitState.Visited, todaysVisits[0].CheckInAt, recorded);
    }
}
