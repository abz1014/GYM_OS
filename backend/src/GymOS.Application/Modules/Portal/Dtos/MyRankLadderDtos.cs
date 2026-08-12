namespace GymOS.Application.Modules.Portal.Dtos;

/// <summary>
/// One rung of the ladder, served rather than hard-coded in the client.
/// </summary>
/// <param name="XpRequired">Lifetime XP at which this rung opens, straight from RankPolicy.</param>
/// <param name="Reached">True for every rung at or below the member's peak.</param>
/// <param name="IsYou">True for exactly one rung — the one they are standing on.</param>
public record MyRankRungDto(string Tier, long XpRequired, bool Reached, bool IsYou);

/// <summary>
/// One member on the same rung. Names are the shortened display form the public board already uses;
/// no ids are exposed, and only members of the caller's own branch appear.
/// </summary>
public record MyRankPeerDto(int Position, string DisplayName, long Xp, bool IsYou);

/// <summary>
/// The member one place above, and the gap. Null when the caller already leads their rung — inventing
/// somebody to chase would be the one number on this screen that is not real.
/// </summary>
public record MyRankChaseDto(string DisplayName, long XpAhead);

/// <summary>Where XP actually came from, over the pace window. Reason is the XpReason name.</summary>
public record MyXpSourceDto(string Reason, long Xp);

/// <summary>One thing this member could do to climb faster — see RankClimbPolicy.</summary>
public record MyClimbTipDto(string Code, string Title, string Detail, int XpValue);

/// <summary>
/// The rank screen's second call: the shape of the ladder, the race the member is actually in, how
/// fast they are moving and what would move them faster. See GetMyRankLadderQuery.
/// </summary>
/// <param name="XpPerWeek">Averaged over <paramref name="PaceWindowDays"/>. Zero is a real answer.</param>
/// <param name="WeeksToNextTier">Null at the top of the ladder, and null when the pace is too slow
/// for an estimate to mean anything — never a number invented to fill the space.</param>
/// <param name="WorkoutsInWindow">Sessions in the same window, so the screen can say what the tips
/// are measured against instead of asserting them.</param>
public record MyRankLadderDto(
    IReadOnlyList<MyRankRungDto> Rungs,
    IReadOnlyList<MyRankPeerDto> OnYourRung,
    MyRankChaseDto? Chasing,
    int XpPerWeek,
    int? WeeksToNextTier,
    int PaceWindowDays,
    int WorkoutsInWindow,
    IReadOnlyList<MyXpSourceDto> XpSources,
    IReadOnlyList<MyClimbTipDto> Tips);
