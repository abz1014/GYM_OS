namespace GymOS.Application.Modules.Engagement.Dtos;

/// <summary>How many active members, in the caller's accessible branches, sit at each level —
/// the shape a manager scans to see whether the roster is climbing or stuck at the bottom.</summary>
public record LevelDistributionRowDto(int Level, int MemberCount);

/// <summary>The engagement/retention correlation the design calls for: are members flagged at risk
/// of churning (ChurnRiskPolicy) also the ones with the lowest game-layer engagement. Either average
/// is 0 when its group is empty, rather than throwing on an empty-sequence Average().</summary>
public record RetentionCorrelationDto(int AtRiskMemberCount, double AtRiskAverageLevel, int ActiveMemberCount, double ActiveAverageLevel);

/// <summary>The manager engagement dashboard in one round trip: how much XP the roster is earning,
/// how many are mid-streak, challenge participation, level spread, and the retention correlation
/// against the existing at-risk report. Branch-isolated like every other staff-wide read.</summary>
public record EngagementSummaryDto(
    int TotalActiveMembers,
    long XpEarnedLast30Days,
    int MembersWithActiveStreak,
    int ChallengeParticipants,
    int ChallengeCompletions,
    List<LevelDistributionRowDto> LevelDistribution,
    RetentionCorrelationDto Retention);
