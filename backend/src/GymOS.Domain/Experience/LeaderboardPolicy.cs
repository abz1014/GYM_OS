namespace GymOS.Domain.Experience;

/// <summary>What a leaderboard is ranked on. Each maps to a ledger the engine already writes.</summary>
public enum LeaderboardCategory
{
    XpEarned,
    WorkoutsLogged,
    GymVisits,
    WeeklyStreak
}

/// <summary>The window a board covers.</summary>
public enum LeaderboardPeriod
{
    Month,
    Week
}

/// <summary>One member's standing, before display names are attached.</summary>
public record LeaderboardStanding(Guid MemberId, int Score, int Rank);

/// <summary>
/// Ranking rules for the member leaderboard.
///
/// Pure, because the interesting parts are decisions rather than queries: how ties are handled, how
/// a member's display name is shortened, and what "top of the board" means for someone who is
/// nowhere near it.
///
/// Deliberately NOT a stored projection — every category is a count over a ledger the engine already
/// maintains (XP transactions, workout logs, attendance, streaks), so a board is computed on read
/// and can never drift from the data it claims to summarise.
/// </summary>
public static class LeaderboardPolicy
{
    /// <summary>How many members appear on the podium.</summary>
    public const int PodiumSize = 3;

    /// <summary>Members shown either side of you when you're outside the podium.</summary>
    public const int NeighbourRadius = 2;

    /// <summary>
    /// Ranks members by score, highest first, using competition ranking: equal scores share a rank
    /// and the next distinct score skips ahead (1, 2, 2, 4). Dense ranking would tell the fourth
    /// person they came third, which stops being motivating the moment they compare notes.
    ///
    /// Members with a zero score are dropped: a board padded with everyone who did nothing makes
    /// the ranking meaningless and quietly tells most of the gym they are last.
    /// </summary>
    public static List<LeaderboardStanding> Rank(IEnumerable<(Guid MemberId, int Score)> scores)
    {
        var ordered = scores
            .Where(s => s.Score > 0)
            // MemberId is the tiebreak purely so the order is deterministic across requests —
            // without it, two members on the same score could swap places between refreshes.
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.MemberId)
            .ToList();

        var standings = new List<LeaderboardStanding>(ordered.Count);
        var rank = 0;
        var previousScore = int.MinValue;

        for (var i = 0; i < ordered.Count; i++)
        {
            if (ordered[i].Score != previousScore)
            {
                rank = i + 1;
                previousScore = ordered[i].Score;
            }

            standings.Add(new LeaderboardStanding(ordered[i].MemberId, ordered[i].Score, rank));
        }

        return standings;
    }

    /// <summary>
    /// The slice of the board shown around a member who didn't make the podium, so they always see
    /// themselves in context rather than just being told a number.
    /// </summary>
    public static List<LeaderboardStanding> NeighboursOf(List<LeaderboardStanding> standings, Guid memberId)
    {
        var index = standings.FindIndex(s => s.MemberId == memberId);
        if (index < 0)
        {
            return [];
        }

        var start = Math.Max(0, index - NeighbourRadius);
        var end = Math.Min(standings.Count - 1, index + NeighbourRadius);

        return standings.GetRange(start, end - start + 1);
    }

    /// <summary>
    /// How far up the board a member sits, as a percentile where 100 is first. Reported instead of a
    /// bare rank because "top 15%" reads as an achievement at any gym size, whereas "37th" reads as
    /// a failure at a big one and a triumph at a tiny one.
    /// </summary>
    public static int PercentileFor(int rank, int totalRanked)
    {
        if (totalRanked <= 0 || rank <= 0)
        {
            return 0;
        }

        // Ties share a rank, so this is "how many people you are ahead of or level with".
        return (int)Math.Round((totalRanked - rank + 1) / (double)totalRanked * 100, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// A member's name as other members see it: first name plus a last initial.
    ///
    /// A leaderboard is the one place this app shows a member to other members, so it shows the
    /// least that still identifies someone to people who train alongside them. Full surnames would
    /// turn an opt-out-less feature into a directory of everyone who attends the gym.
    /// </summary>
    public static string DisplayName(string firstName, string? lastName)
    {
        var first = (firstName ?? string.Empty).Trim();
        var last = (lastName ?? string.Empty).Trim();

        if (first.Length == 0)
        {
            return last.Length > 0 ? last : "Member";
        }

        return last.Length > 0 ? $"{first} {char.ToUpperInvariant(last[0])}." : first;
    }

    /// <summary>The inclusive date window a period covers, relative to today.</summary>
    public static (DateOnly Start, DateOnly End) WindowFor(LeaderboardPeriod period, DateOnly today)
        => period == LeaderboardPeriod.Week
            ? (StreakCalculatorWeekStart(today), StreakCalculatorWeekStart(today).AddDays(6))
            : (new DateOnly(today.Year, today.Month, 1), new DateOnly(today.Year, today.Month, 1).AddMonths(1).AddDays(-1));

    /// <summary>Monday-start, matching every other week in this system.</summary>
    private static DateOnly StreakCalculatorWeekStart(DateOnly date)
        => GymOS.Domain.Members.StreakCalculator.WeekStart(date);
}
