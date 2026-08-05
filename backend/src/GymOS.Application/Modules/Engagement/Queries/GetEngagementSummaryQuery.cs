using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Common.Security;
using GymOS.Application.Modules.Engagement.Dtos;
using GymOS.Application.Modules.Reports.Queries;
using GymOS.Domain.Members;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Engagement.Queries;

/// <summary>
/// The manager engagement dashboard (blueprint Phase 13): how much of the Member Experience Engine
/// the caller's accessible branches are actually using, and whether that engagement correlates with
/// retention. Reuses GetAtRiskMembersReportQuery's own logic for "at risk" rather than re-deriving
/// the ChurnRiskPolicy threshold — one definition, extended here with a branch filter the existing
/// tenant-wide report doesn't apply, so this dashboard stays branch-isolated like every other MEE read.
/// </summary>
public record GetEngagementSummaryQuery : IQuery<EngagementSummaryDto>;

public class GetEngagementSummaryQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetEngagementSummaryQuery, EngagementSummaryDto>
{
    public async Task<EngagementSummaryDto> Handle(GetEngagementSummaryQuery request, CancellationToken cancellationToken)
    {
        var accessibleBranchIds = await BranchAccessResolver.GetAccessibleBranchIdsAsync(db, currentUser, cancellationToken);

        var memberIds = await db.Members.AsNoTracking()
            .Where(m => accessibleBranchIds.Contains(m.BranchId) && m.Status == MemberStatus.Active)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

        if (memberIds.Count == 0)
        {
            return new EngagementSummaryDto(0, 0, 0, 0, 0, [], new RetentionCorrelationDto(0, 0, 0, 0));
        }

        // Combining .Contains() with a DateTimeOffset range filter in one SQLite query doesn't
        // translate (same class of issue as the DateOnly-window filters elsewhere in MEE) — pull this
        // member set's transactions first, then apply the 30-day window in memory.
        var thirtyDaysAgo = dateTimeProvider.UtcNow.AddDays(-30);
        var xpEarned = (await db.XpTransactions.AsNoTracking()
                .Where(t => memberIds.Contains(t.MemberId))
                .Select(t => new { t.Amount, t.OccurredAt })
                .ToListAsync(cancellationToken))
            .Where(t => t.OccurredAt >= thirtyDaysAgo)
            .Sum(t => (long)t.Amount);

        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);
        var checkInDatesByMember = (await db.AttendanceRecords.AsNoTracking()
                .Where(a => memberIds.Contains(a.MemberId))
                .Select(a => new { a.MemberId, a.CheckInAt })
                .ToListAsync(cancellationToken))
            .GroupBy(a => a.MemberId)
            .ToDictionary(g => g.Key, g => g.Select(x => DateOnly.FromDateTime(x.CheckInAt.UtcDateTime)).Distinct().ToList());
        var membersWithActiveStreak = memberIds.Count(id =>
            checkInDatesByMember.TryGetValue(id, out var dates) && StreakCalculator.CurrentWeeklyStreak(dates, today) > 0);

        var challengeParticipants = await db.ChallengeParticipants.AsNoTracking()
            .Where(p => memberIds.Contains(p.MemberId))
            .Select(p => p.MemberId)
            .Distinct()
            .CountAsync(cancellationToken);
        var challengeCompletions = await db.ChallengeParticipants.AsNoTracking()
            .CountAsync(p => memberIds.Contains(p.MemberId) && p.IsCompleted, cancellationToken);

        // Level defaults to 1 for a member with no MemberProgression row yet (never earned XP) — the
        // same default the entity itself starts at, so the distribution counts the whole roster, not
        // just members who've already engaged.
        var levelByMember = await db.MemberProgressions.AsNoTracking()
            .Where(p => memberIds.Contains(p.MemberId))
            .Select(p => new { p.MemberId, p.Level })
            .ToDictionaryAsync(p => p.MemberId, p => p.Level, cancellationToken);

        var levelDistribution = memberIds
            .Select(id => levelByMember.GetValueOrDefault(id, 1))
            .GroupBy(level => level)
            .Select(g => new LevelDistributionRowDto(g.Key, g.Count()))
            .OrderBy(r => r.Level)
            .ToList();

        var atRiskMemberIds = (await GetAtRiskMembersReportQueryHandler.BuildAsync(db, dateTimeProvider, cancellationToken))
            .Select(r => r.MemberId)
            .Where(memberIds.Contains)
            .ToHashSet();

        var atRiskLevels = atRiskMemberIds.Select(id => levelByMember.GetValueOrDefault(id, 1)).ToList();
        var activeMemberIds = memberIds.Where(id => !atRiskMemberIds.Contains(id)).ToList();
        var activeLevels = activeMemberIds.Select(id => levelByMember.GetValueOrDefault(id, 1)).ToList();

        var retention = new RetentionCorrelationDto(
            atRiskMemberIds.Count,
            atRiskLevels.Count > 0 ? atRiskLevels.Average() : 0,
            activeMemberIds.Count,
            activeLevels.Count > 0 ? activeLevels.Average() : 0);

        return new EngagementSummaryDto(
            memberIds.Count, xpEarned, membersWithActiveStreak, challengeParticipants, challengeCompletions,
            levelDistribution, retention);
    }
}
