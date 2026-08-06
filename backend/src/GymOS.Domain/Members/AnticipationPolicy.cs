namespace GymOS.Domain.Members;

/// <summary>What kind of thing a member has coming.</summary>
public enum AnticipationKind
{
    /// <summary>A class they have already booked.</summary>
    BookedClass,

    /// <summary>A challenge they joined and are close to finishing.</summary>
    Challenge,

    /// <summary>The next level, when it is genuinely within reach.</summary>
    Level
}

/// <summary>The one thing worth looking forward to, said plainly.</summary>
public readonly record struct Anticipation(AnticipationKind Kind, string Title, string Detail);

/// <summary>
/// Everything the app knows about what is ahead of a member. All optional: a member with none of it
/// simply has nothing coming, which is a true answer and better than a manufactured one.
/// </summary>
public readonly record struct AnticipationSignals(
    string? NextClassName,
    DateTimeOffset? NextClassAt,
    string? ChallengeName,
    int ChallengeSessionsRemaining,
    int NextLevel,
    long XpToNextLevel,
    long XpPerSession);

/// <summary>
/// The single nearest thing a member has to look forward to.
///
/// Everything else on the home screen looks backwards — what you did, how the week is going, what
/// your streak is. None of it answers "why open this again", and a member who has met their goal is
/// shown a closed ring and nothing beyond it.
///
/// The rule throughout is that anticipation has to be REAL and NEAR. A challenge nine sessions off
/// is not something to look forward to, it is a chore; a level four hundred XP away is a number, not
/// a milestone. Anything outside reach is dropped rather than dressed up, and a member with nothing
/// close is told nothing — the app does not invent a reason to come back, because a fabricated one
/// is discovered exactly once and costs the trust every honest number here was built to earn.
///
/// Ranked by commitment, not by size. A booked class beats everything because the member already
/// decided to be there; it is the only item that is a promise rather than a possibility.
/// </summary>
public static class AnticipationPolicy
{
    /// <summary>Beyond this many sessions a challenge stops being "nearly there".</summary>
    public const int MaxSessionsAway = 3;

    /// <summary>A level is near when it is within about this many sessions of training.</summary>
    public const int MaxSessionsToLevel = 3;

    public static Anticipation? Next(AnticipationSignals signals, DateTimeOffset now)
    {
        // 1. Something already committed to. A booked class is the only one of these a member has
        //    actually promised themselves, and a promise beats a possibility.
        if (signals.NextClassName is { Length: > 0 } className && signals.NextClassAt is DateTimeOffset at && at >= now)
        {
            return new Anticipation(AnticipationKind.BookedClass, className, WhenPhrase(at, now));
        }

        // 2. A challenge they opted into and can finish soon. Not one they merely could join — the
        //    member chose this, so finishing it means something to them specifically.
        if (signals.ChallengeName is { Length: > 0 } challenge
            && signals.ChallengeSessionsRemaining > 0
            && signals.ChallengeSessionsRemaining <= MaxSessionsAway)
        {
            var sessions = signals.ChallengeSessionsRemaining;
            return new Anticipation(
                AnticipationKind.Challenge,
                challenge,
                sessions == 1 ? "One more session finishes it." : $"{sessions} more sessions finish it.");
        }

        // 3. The next level, but only when training a few more times would actually get there.
        //    Expressed in sessions rather than XP: a member counts workouts, not points.
        if (signals.XpToNextLevel > 0 && signals.XpPerSession > 0)
        {
            var sessions = (int)Math.Ceiling(signals.XpToNextLevel / (double)signals.XpPerSession);
            if (sessions <= MaxSessionsToLevel)
            {
                return new Anticipation(
                    AnticipationKind.Level,
                    $"Level {signals.NextLevel}",
                    sessions == 1 ? "One more session reaches it." : $"About {sessions} more sessions.");
            }
        }

        return null;
    }

    /// <summary>
    /// When a class is, in the words someone would use out loud. Days are counted on the calendar
    /// rather than in 24-hour blocks, so a class tomorrow morning reads as "tomorrow" and not "today"
    /// because it happens to be less than a day away.
    /// </summary>
    private static string WhenPhrase(DateTimeOffset at, DateTimeOffset now)
    {
        var days = at.Date.Subtract(now.Date).Days;
        return days switch
        {
            <= 0 => $"Today at {at:h:mm tt}",
            1 => $"Tomorrow at {at:h:mm tt}",
            < 7 => $"{at:dddd} at {at:h:mm tt}",
            _ => $"{at:ddd d MMM} at {at:h:mm tt}"
        };
    }
}
