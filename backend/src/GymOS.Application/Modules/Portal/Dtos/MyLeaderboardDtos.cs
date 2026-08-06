namespace GymOS.Application.Modules.Portal.Dtos;

/// <summary>One place on the board. Names are already shortened for display; no ids are exposed.</summary>
public record LeaderboardRowDto(int Rank, string DisplayName, int Score, bool IsYou);

/// <summary>
/// A leaderboard as one member sees it.
///
/// Returns the podium plus a slice around the caller rather than the whole gym: the full board is
/// unbounded, most of it is irrelevant to any one member, and shipping every member's activity to
/// every other member is a privacy question nobody asked for. What a member gets is the top of the
/// board, where they sit in it, and who is immediately either side of them.
/// </summary>
/// <param name="You">The caller's own row, or null when they haven't scored in this window.</param>
/// <param name="YourPercentile">100 is first place. Null when the caller isn't ranked.</param>
public record MyLeaderboardDto(
    string Category,
    string Period,
    DateOnly WindowStart,
    DateOnly WindowEnd,
    int TotalRanked,
    IReadOnlyList<LeaderboardRowDto> Podium,
    IReadOnlyList<LeaderboardRowDto> AroundYou,
    LeaderboardRowDto? You,
    int? YourPercentile);
