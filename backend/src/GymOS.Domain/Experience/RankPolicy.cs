namespace GymOS.Domain.Experience;

/// <summary>
/// The named rungs of the ladder. Ordered, and the integer values are load-bearing — demotion steps
/// down by one, so the sequence must stay contiguous and ascending.
/// </summary>
public enum RankTier
{
    Newcomer = 0,
    Regular = 1,
    Committed = 2,
    Strong = 3,
    Relentless = 4,
    Elite = 5,
    Titan = 6,
    Legend = 7
}

/// <summary>A member's standing: what they have ever reached, and where they stand today.</summary>
/// <param name="Peak">The highest tier ever held. Never falls.</param>
/// <param name="Current">Where they stand now. Falls with prolonged absence, returns when they do.</param>
/// <param name="TiersLostToAbsence">How many rungs absence has cost — 0 when current and peak agree.</param>
public readonly record struct RankStanding(RankTier Peak, RankTier Current, int TiersLostToAbsence);

/// <summary>
/// Names the number.
///
/// A member's progression was an integer and nothing else: "Level 7" — of what, and what is above me?
/// Valorant does not say "Rank 12", it says Immortal, and that difference is most of the feeling. XP
/// already aggregates everything a member does — sessions, visits, nutrition, recovery, beating a
/// personal best — so it is the honest basis for "did you show up and put the work in".
///
/// TWO RANKS, ON PURPOSE.
///
/// Peak is what you reached, and it is permanent. It reads off PeakXp, which ratchets, so nothing —
/// not an undo, not injury, not a year away — can take it back. That is the promise the progression
/// entity now enforces at the field level.
///
/// Current is where you stand, and it can fall. Without that, "rank" in a gym is just a total that
/// only ever grows, which is a scoreboard, not a standing.
///
/// DEMOTION IS ABSENCE, NEVER LOSS. A member never drops a tier for a bad session, an injury week, or
/// undoing a mis-tap — only for not being there at all, and only after a fortnight's grace. This is
/// the one place a fitness product must part company with the games it borrows from: in Valorant
/// losing rank is the stake that makes winning mean something, but a gym member is a real person who
/// gets ill, travels, or is starting again at fifty. Punishing them at the exact moment they are
/// deciding whether to come back is precisely backwards, and the moment retention is most fragile.
///
/// So returning restores everything immediately. One session and Current is back level with Peak. The
/// demotion is a pull, not a penalty: what a lapsed member sees is "you were Strong — one session
/// gets you back", which is an invitation. Making them re-earn it would be the version that reads as
/// punishment, and the version that loses them.
/// </summary>
public static class RankPolicy
{
    /// <summary>
    /// Lifetime XP at which each tier opens. Calibrated against what the XP rules actually pay: an
    /// engaged member earning sessions, visits and nutrition runs roughly 300 XP a week, so Regular
    /// lands within a few weeks, Committed inside a couple of months, and Legend is a multi-year
    /// claim rather than a participation trophy. A ladder whose top rung is reachable in a month is a
    /// ladder nobody looks up at.
    /// </summary>
    private static readonly (RankTier Tier, long Xp)[] Thresholds =
    [
        (RankTier.Newcomer, 0),
        (RankTier.Regular, 750),
        (RankTier.Committed, 2_500),
        (RankTier.Strong, 6_000),
        (RankTier.Relentless, 12_000),
        (RankTier.Elite, 20_000),
        (RankTier.Titan, 32_000),
        (RankTier.Legend, 50_000)
    ];

    /// <summary>Days away before absence costs anything. A fortnight covers a holiday, a bad flu, or a
    /// deload week without ever telling somebody they have slipped.</summary>
    public const int GraceDays = 14;

    /// <summary>Days of continued absence per tier lost, after the grace period.</summary>
    public const int DaysPerTierLost = 28;

    public static long XpRequiredFor(RankTier tier) => Thresholds[(int)tier].Xp;

    /// <summary>The tier a given lifetime XP total has earned.</summary>
    public static RankTier TierFor(long xp)
    {
        var tier = RankTier.Newcomer;
        foreach (var (candidate, required) in Thresholds)
        {
            if (xp >= required)
            {
                tier = candidate;
            }
        }

        return tier;
    }

    /// <summary>The next rung up, or null at the top. Legend is the end of the ladder, and saying so
    /// plainly beats inventing a progress bar that can never fill.</summary>
    public static RankTier? NextTierAfter(RankTier tier) =>
        tier == RankTier.Legend ? null : tier + 1;

    /// <summary>
    /// Progress toward the next rung, as XP earned into the band and the size of the band.
    /// Returns (0, 0) at Legend, where there is nothing left to fill.
    /// </summary>
    public static (long IntoTier, long TierSpan) ProgressWithin(long xp)
    {
        var tier = TierFor(xp);
        var next = NextTierAfter(tier);
        if (next is null)
        {
            return (0, 0);
        }

        var floor = XpRequiredFor(tier);
        return (xp - floor, XpRequiredFor(next.Value) - floor);
    }

    /// <summary>
    /// Where a member stands, given everything they have earned and how long since they were last
    /// seen. <paramref name="daysSinceLastActivity"/> is null for a member who has never logged
    /// anything — a brand new member is not absent, they simply have not started.
    /// </summary>
    public static RankStanding StandingFor(long peakXp, int? daysSinceLastActivity)
    {
        var peak = TierFor(peakXp);

        var beyondGrace = (daysSinceLastActivity ?? 0) - GraceDays;
        if (beyondGrace <= 0)
        {
            return new RankStanding(peak, peak, 0);
        }

        // Integer division, so the first tier is lost only once a FULL DaysPerTierLost has elapsed
        // past the grace period — never on the day it tips over.
        var tiersLost = beyondGrace / DaysPerTierLost;
        if (tiersLost <= 0)
        {
            return new RankStanding(peak, peak, 0);
        }

        var currentIndex = (int)peak - tiersLost;
        var current = currentIndex <= (int)RankTier.Newcomer ? RankTier.Newcomer : (RankTier)currentIndex;

        // Report what was actually lost, not what the arithmetic wanted to take: someone whose peak is
        // Regular cannot lose five tiers, and telling them they had would be a fiction.
        return new RankStanding(peak, current, (int)peak - (int)current);
    }
}
