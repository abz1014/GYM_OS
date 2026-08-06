using GymOS.Application.Common.Interfaces;
using GymOS.Domain.Common;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Portal.Dtos;
using GymOS.Domain.Experience;
using GymOS.Domain.Members;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Portal.Queries;

/// <summary>
/// The member leaderboard, scoped to the caller's own branch.
///
/// Branch rather than tenant on purpose: a leaderboard is only motivating against people you might
/// actually run into, and a multi-site gym's members have no relationship with another site's.
///
/// Computed on read from the ledgers the engine already maintains — no stored board, so it can never
/// drift from the data it summarises. Self-scoped via MyMemberResolver: the caller's branch is
/// resolved server-side and no member id is accepted, so nobody can request another branch's board.
/// </summary>
public record GetMyLeaderboardQuery(LeaderboardCategory Category, LeaderboardPeriod Period) : IQuery<MyLeaderboardDto>;

public class GetMyLeaderboardQueryHandler(
    IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetMyLeaderboardQuery, MyLeaderboardDto>
{
    public async Task<MyLeaderboardDto> Handle(GetMyLeaderboardQuery request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);
        var zone = await MyMemberResolver.ResolveGymZoneAsync(db, memberId, cancellationToken);
        var today = GymDay.Of(dateTimeProvider.UtcNow, zone);
        var (windowStart, windowEnd) = LeaderboardPolicy.WindowFor(request.Period, today);

        var branchId = await db.Members.AsNoTracking()
            .Where(m => m.Id == memberId).Select(m => m.BranchId).FirstAsync(cancellationToken);

        // Everyone this member is ranked against. Members carry the tenant filter already; BranchId
        // is applied explicitly because that filter is tenant-wide, not branch-wide.
        var roster = await db.Members.AsNoTracking()
            .Where(m => m.BranchId == branchId && m.Status == MemberStatus.Active)
            .Select(m => new { m.Id, m.FirstName, m.LastName })
            .ToListAsync(cancellationToken);

        var memberIds = roster.Select(r => r.Id).ToList();
        var scores = await ScoreAsync(request.Category, memberIds, windowStart, windowEnd, today, zone, cancellationToken);

        var standings = LeaderboardPolicy.Rank(scores);
        var namesById = roster.ToDictionary(r => r.Id, r => LeaderboardPolicy.DisplayName(r.FirstName, r.LastName));
        LeaderboardRowDto Row(LeaderboardStanding s) =>
            new(s.Rank, namesById.GetValueOrDefault(s.MemberId, "Member"), s.Score, s.MemberId == memberId);

        var mine = standings.FirstOrDefault(s => s.MemberId == memberId);
        var podium = standings.Take(LeaderboardPolicy.PodiumSize).Select(Row).ToList();

        // Only bother with the neighbour strip when the member isn't already visible on the podium.
        var aroundYou = mine is not null && mine.Rank > LeaderboardPolicy.PodiumSize
            ? LeaderboardPolicy.NeighboursOf(standings, memberId).Select(Row).ToList()
            : [];

        return new MyLeaderboardDto(
            request.Category.ToString(),
            request.Period.ToString(),
            windowStart,
            windowEnd,
            standings.Count,
            podium,
            aroundYou,
            mine is null ? null : Row(mine),
            mine is null ? null : LeaderboardPolicy.PercentileFor(mine.Rank, standings.Count));
    }

    /// <summary>
    /// One member, one score. Every branch here pulls its rows to memory before filtering by date:
    /// SQLite (the test provider) can't range-filter a DateTimeOffset alongside a parameterised
    /// Contains, and the queries are already bounded to a single branch's roster.
    /// </summary>
    private async Task<List<(Guid MemberId, int Score)>> ScoreAsync(
        LeaderboardCategory category, List<Guid> memberIds,
        DateOnly windowStart, DateOnly windowEnd, DateOnly today, TimeZoneInfo zone, CancellationToken cancellationToken)
    {
        bool InWindow(DateTimeOffset at)
        {
            var d = GymDay.Of(at, zone);
            return d >= windowStart && d <= windowEnd;
        }

        switch (category)
        {
            case LeaderboardCategory.XpEarned:
            {
                var rows = await db.XpTransactions.AsNoTracking()
                    .Where(x => memberIds.Contains(x.MemberId))
                    .Select(x => new { x.MemberId, x.Amount, x.OccurredAt })
                    .ToListAsync(cancellationToken);

                return rows.Where(r => InWindow(r.OccurredAt))
                    .GroupBy(r => r.MemberId)
                    .Select(g => (g.Key, g.Sum(r => r.Amount)))
                    .ToList();
            }

            case LeaderboardCategory.WorkoutsLogged:
            {
                var rows = await db.WorkoutLogs.AsNoTracking()
                    .Where(w => memberIds.Contains(w.MemberId))
                    .Select(w => new { w.MemberId, w.LoggedAt })
                    .ToListAsync(cancellationToken);

                // Distinct DAYS, matching WeeklyGoalPolicy's definition of a session — otherwise
                // splitting one workout into five entries would outrank someone who trained twice.
                return rows.Where(r => InWindow(r.LoggedAt))
                    .GroupBy(r => r.MemberId)
                    .Select(g => (g.Key, g.Select(r => GymDay.Of(r.LoggedAt, zone)).Distinct().Count()))
                    .ToList();
            }

            case LeaderboardCategory.GymVisits:
            {
                var rows = await db.AttendanceRecords.AsNoTracking()
                    .Where(a => memberIds.Contains(a.MemberId))
                    .Select(a => new { a.MemberId, a.CheckInAt })
                    .ToListAsync(cancellationToken);

                // Also per day: two check-ins on one day is one visit, not two.
                return rows.Where(r => InWindow(r.CheckInAt))
                    .GroupBy(r => r.MemberId)
                    .Select(g => (g.Key, g.Select(r => GymDay.Of(r.CheckInAt, zone)).Distinct().Count()))
                    .ToList();
            }

            case LeaderboardCategory.WeeklyStreak:
            {
                // A streak is a standing value, not something accumulated inside a window, so this
                // category ignores the period toggle by design and always reports the current run.
                var rows = await db.WorkoutLogs.AsNoTracking()
                    .Where(w => memberIds.Contains(w.MemberId))
                    .Select(w => new { w.MemberId, w.LoggedAt })
                    .ToListAsync(cancellationToken);

                return rows.GroupBy(r => r.MemberId)
                    .Select(g => (g.Key, StreakCalculator.CurrentWeeklyStreak(
                        g.Select(r => GymDay.Of(r.LoggedAt, zone)), today)))
                    .ToList();
            }

            default:
                return [];
        }
    }
}
