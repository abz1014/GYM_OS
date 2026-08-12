namespace GymOS.Domain.Experience;

/// <summary>
/// How fast a member is climbing, and roughly when they arrive. Null when there is nothing to say.
/// </summary>
/// <param name="XpPerWeek">Average over the sample window. Zero for someone who has not trained.</param>
/// <param name="WeeksToNextTier">Null at the top of the ladder, and null at zero pace — an ETA
/// computed from no movement is a division by zero dressed up as encouragement.</param>
public readonly record struct ClimbPace(int XpPerWeek, int? WeeksToNextTier);

/// <summary>One concrete thing a member could do to climb faster, and what it is actually worth.</summary>
/// <param name="Code">Stable identifier, for the client to pick an icon and a destination.</param>
/// <param name="Title">The action, in the imperative.</param>
/// <param name="Detail">Why it is being suggested to THIS member — always a real number from their
/// own record, never a generic tip.</param>
/// <param name="XpValue">What the action pays, straight from <see cref="XpPolicy"/>.</param>
public record ClimbTip(string Code, string Title, string Detail, int XpValue);

/// <summary>
/// What a member is standing on, so the screen can show a real position rather than a mood.
/// </summary>
public readonly record struct RankRivalGap(int Rank, int Total, long XpBehindNext, string? NextName);

/// <summary>
/// The maths behind "how do I climb, and how long will it take?".
///
/// The rank screen was a number and a ladder: true, and inert. What makes a rank feel worth reaching
/// in a game is not the badge, it is knowing exactly how far away it is, who is just above you, and
/// which specific thing you are not doing. All three are computable here from data the engine already
/// keeps, which is the constraint this whole policy is written under — every sentence the screen says
/// has to be traceable to a row in a table.
///
/// So there is deliberately no motivational copy in here. A tip only exists when the member's own
/// record shows the gap it is pointing at, and it always carries the XP the action really pays. The
/// alternative — a fixed list of encouragements — would be the app telling everybody the same thing
/// and calling it coaching.
/// </summary>
public static class RankClimbPolicy
{
    /// <summary>
    /// Days of history the pace is averaged over. Four weeks: long enough that one missed week does
    /// not halve the estimate, short enough that it still reflects what the member is doing NOW
    /// rather than what they did last spring.
    /// </summary>
    public const int PaceWindowDays = 28;

    /// <summary>
    /// A member earning less than this is treated as not climbing, and gets no ETA.
    ///
    /// The threshold is one gym visit a week. Below it the arithmetic still produces a number, and
    /// that number is the problem: 15 XP a week against a 5,500 XP gap is "seven years", which is
    /// arithmetically correct, useless, and actively discouraging at the exact moment somebody is
    /// coming back. Where there is no real pace the screen says so instead.
    /// </summary>
    public const int MinimumMeaningfulXpPerWeek = 20;

    /// <summary>Beyond this, an ETA stops being information. Two years of weeks is "keep going".</summary>
    public const int MaxReportableWeeks = 104;

    public static ClimbPace PaceFor(long xpInWindow, long xpToNextTier, bool atTopOfLadder)
    {
        var xpPerWeek = (int)Math.Round(xpInWindow / (PaceWindowDays / 7.0), MidpointRounding.AwayFromZero);

        if (atTopOfLadder || xpPerWeek < MinimumMeaningfulXpPerWeek || xpToNextTier <= 0)
        {
            return new ClimbPace(Math.Max(0, xpPerWeek), null);
        }

        var weeks = (int)Math.Ceiling(xpToNextTier / (double)xpPerWeek);
        return new ClimbPace(xpPerWeek, weeks > MaxReportableWeeks ? null : weeks);
    }

    /// <summary>
    /// What the member actually DID over the pace window, counted from the records themselves.
    ///
    /// Deliberately not the XP ledger. XP is the accounting of an action, and the two come apart:
    /// seeded history is written straight to the tables without raising the events that award XP, an
    /// award rule can be added after members have already been doing the thing, and a projection
    /// rebuild is not instantaneous. Reading tips off the ledger produced "12 of your last 12
    /// sessions had no check-in against them" for a member with twenty-six check-ins on file —
    /// a sentence that is not merely useless but demonstrably false to the person reading it.
    ///
    /// So: XP measures XP (that is what pace is), and behaviour measures behaviour.
    /// </summary>
    public readonly record struct ClimbActivity(
        int Workouts,
        int CheckIns,
        int PersonalRecords,
        int DaysWithMealsLogged,
        int RecoveryDays,
        bool InActiveChallenge);

    /// <summary>
    /// Ways this member — not members in general — could earn more.
    ///
    /// Every branch is a comparison against their own last four weeks, and every one is worded as an
    /// opportunity rather than a failure. Ordered most-valuable first, so a screen showing three
    /// shows the three worth most.
    /// </summary>
    public static IReadOnlyList<ClimbTip> TipsFor(ClimbActivity activity)
    {
        var tips = new List<ClimbTip>();

        // Worth the most, and the one most members never notice exists.
        if (!activity.InActiveChallenge)
        {
            tips.Add(new ClimbTip(
                "challenge",
                "Join a challenge",
                "You are not in one right now. Finishing a challenge is the single biggest award in the app.",
                XpPolicy.AwardFor(XpReason.ChallengeCompleted)));
        }

        // The most common miss by a distance: members who log a workout from home and never check in.
        // Only raised when they have actually been training, so it reads as "you are leaving this on
        // the table" rather than "you never come".
        if (activity.Workouts > 0 && activity.CheckIns < activity.Workouts)
        {
            var missed = activity.Workouts - activity.CheckIns;
            tips.Add(new ClimbTip(
                "check-in",
                "Check in when you arrive",
                $"{missed} of your last {activity.Workouts} sessions had no check-in against them.",
                XpPolicy.AwardFor(XpReason.GymVisit)));
        }

        // Progressive overload. Suggested only to somebody already training — telling a member who
        // has not been in for a month to add weight is answering a question they are not asking.
        if (activity.Workouts > 0 && activity.PersonalRecords == 0)
        {
            tips.Add(new ClimbTip(
                "progress",
                "Beat one of your bests",
                $"No personal record in your last {activity.Workouts} sessions. One heavier set counts.",
                XpPolicy.AwardFor(XpReason.ProgressiveImprovement)));
        }

        if (activity.DaysWithMealsLogged == 0)
        {
            tips.Add(new ClimbTip(
                "nutrition",
                "Follow your nutrition plan",
                "Nothing logged against it in the last four weeks. It pays every day you stay on it.",
                XpPolicy.AwardFor(XpReason.NutritionAdherence)));
        }

        if (activity.RecoveryDays == 0)
        {
            tips.Add(new ClimbTip(
                "recovery",
                "Log a rest day",
                "Rest counts here. You have not logged one in the last four weeks.",
                XpPolicy.AwardFor(XpReason.RecoveryLogged)));
        }

        return tips.OrderByDescending(t => t.XpValue).ToList();
    }
}
