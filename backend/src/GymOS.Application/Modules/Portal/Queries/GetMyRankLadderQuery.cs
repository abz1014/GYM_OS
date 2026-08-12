using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Portal.Dtos;
using GymOS.Domain.Common;
using GymOS.Domain.Experience;
using GymOS.Domain.Members;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Portal.Queries;

/// <summary>
/// Everything the rank screen needs to make the next rung feel worth reaching: the shape of the
/// ladder, who else is standing on this rung, how fast the member is climbing, and what specifically
/// they could do about it.
///
/// Four things it fixes, all of them a version of "the screen knew where you stood but not what that
/// meant":
///
/// 1. THE LADDER IS SERVED, NOT DUPLICATED. The client held its own copy of RankPolicy's thresholds
///    with a comment admitting the cost. A ladder that disagrees with the engine is a screen telling
///    a member they need 6,000 XP for a rung the server opens at 5,000.
///
/// 2. A LEADERBOARD FOR THE RUNG THEY ARE ON. The existing board ranks the whole branch, where a
///    Newcomer sits ninetieth behind eight Titans and learns nothing. Ranked among people at the same
///    rank, everybody is in a race they could actually win — and the gap to the person directly above
///    is a real number, usually a couple of sessions.
///
/// 3. PACE AND AN ETA. "5,500 XP to Strong" is a distance with no scale. "About nine weeks at your
///    current pace" is a plan. Suppressed rather than faked where there is no pace to measure — see
///    RankClimbPolicy.
///
/// 4. TIPS FROM THEIR OWN RECORD. Every suggestion names a number out of this member's last four
///    weeks and carries what the action really pays.
///
/// Branch-scoped and self-scoped, like every other /api/me read: the caller's branch is resolved
/// server-side, no member id is accepted, and the only names returned are the initial-form ones
/// LeaderboardPolicy already uses for the public board.
/// </summary>
public record GetMyRankLadderQuery : IQuery<MyRankLadderDto>;

public class GetMyRankLadderQueryHandler(
    IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetMyRankLadderQuery, MyRankLadderDto>
{
    public async Task<MyRankLadderDto> Handle(GetMyRankLadderQuery request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);
        var zone = await MyMemberResolver.ResolveGymZoneAsync(db, memberId, cancellationToken);
        var today = GymDay.Of(dateTimeProvider.UtcNow, zone);
        var windowStart = today.AddDays(-RankClimbPolicy.PaceWindowDays);

        var branchId = await db.Members.AsNoTracking()
            .Where(m => m.Id == memberId).Select(m => m.BranchId).FirstAsync(cancellationToken);

        // ---- the ladder, straight off the policy ----
        var myXp = await db.MemberProgressions.AsNoTracking()
            .Where(p => p.MemberId == memberId)
            .Select(p => (long?)p.PeakXp)
            .FirstOrDefaultAsync(cancellationToken) ?? 0;

        var myTier = RankPolicy.TierFor(myXp);
        var nextTier = RankPolicy.NextTierAfter(myTier);
        var xpToNext = nextTier is null ? 0 : RankPolicy.XpRequiredFor(nextTier.Value) - myXp;

        var rungs = Enum.GetValues<RankTier>()
            .Select(tier => new MyRankRungDto(
                tier.ToString(),
                RankPolicy.XpRequiredFor(tier),
                tier <= myTier,
                tier == myTier))
            .ToList();

        // ---- who else is on this rung ----
        //
        // Everyone active at this branch with a progression row, bucketed by the same TierFor the
        // member's own standing came from, so the board can never disagree with the badge above it.
        var roster = await db.Members.AsNoTracking()
            .Where(m => m.BranchId == branchId && m.Status == MemberStatus.Active)
            .Select(m => new { m.Id, m.FirstName, m.LastName })
            .ToListAsync(cancellationToken);

        var rosterIds = roster.Select(r => r.Id).ToList();
        var progressions = await db.MemberProgressions.AsNoTracking()
            .Where(p => rosterIds.Contains(p.MemberId))
            .Select(p => new { p.MemberId, p.PeakXp })
            .ToListAsync(cancellationToken);

        var namesById = roster.ToDictionary(
            r => r.Id, r => LeaderboardPolicy.DisplayName(r.FirstName, r.LastName));

        var onThisRung = progressions
            .Where(p => RankPolicy.TierFor(p.PeakXp) == myTier)
            // Highest first, then by id so two members on identical XP get a stable order rather
            // than one that reshuffles between requests.
            .OrderByDescending(p => p.PeakXp)
            .ThenBy(p => p.MemberId)
            .ToList();

        var myIndex = onThisRung.FindIndex(p => p.MemberId == memberId);
        var rungBoard = onThisRung
            .Select((p, i) => new MyRankPeerDto(
                i + 1,
                namesById.GetValueOrDefault(p.MemberId, "Member"),
                p.PeakXp,
                p.MemberId == memberId))
            .ToList();

        // The person one place above, and what it would take to pass them. Null at the top of the
        // rung, which is the honest answer for somebody already leading it.
        var chasing = myIndex > 0
            ? new MyRankChaseDto(
                namesById.GetValueOrDefault(onThisRung[myIndex - 1].MemberId, "Member"),
                onThisRung[myIndex - 1].PeakXp - myXp)
            : null;

        // ---- pace, and where the XP came from ----
        var xpRows = await db.XpTransactions.AsNoTracking()
            .Where(x => x.MemberId == memberId)
            .Select(x => new { x.Amount, x.Reason, x.OccurredAt })
            .ToListAsync(cancellationToken);

        // Reduced in memory: DateTimeOffset cannot be range-filtered in SQL on SQLite, the same
        // constraint every query in this module works around.
        var inWindow = xpRows
            .Where(x => GymDay.Of(x.OccurredAt, zone) >= windowStart)
            .ToList();

        var xpByReason = inWindow
            .GroupBy(x => x.Reason)
            .ToDictionary(g => g.Key, g => g.Sum(x => (long)x.Amount));

        var pace = RankClimbPolicy.PaceFor(
            inWindow.Sum(x => (long)x.Amount), xpToNext, nextTier is null);

        // ---- what they actually did, counted from the records rather than from the XP ledger ----
        //
        // The two come apart. Seeded history is written straight to the tables without raising the
        // events that award XP, and an award rule can be added long after members have been doing the
        // thing. Reading the tips off the ledger told a member with twenty-six check-ins on file that
        // "12 of your last 12 sessions had no check-in against them" — see RankClimbPolicy.
        //
        // All of these count DISTINCT DAYS, which is what the member means by "a session" or "a day
        // I logged food", and reduce in memory for the usual reason: DateTimeOffset cannot be
        // range-filtered in SQL on SQLite.
        int DaysInWindow(IEnumerable<DateTimeOffset> stamps) => stamps
            .Select(at => GymDay.Of(at, zone))
            .Where(d => d >= windowStart)
            .Distinct()
            .Count();

        var workouts = DaysInWindow(await db.WorkoutLogs.AsNoTracking()
            .Where(w => w.MemberId == memberId).Select(w => w.LoggedAt).ToListAsync(cancellationToken));

        var checkIns = DaysInWindow(await db.AttendanceRecords.AsNoTracking()
            .Where(a => a.MemberId == memberId).Select(a => a.CheckInAt).ToListAsync(cancellationToken));

        var personalRecords = DaysInWindow(await db.PersonalRecords.AsNoTracking()
            .Where(r => r.MemberId == memberId).Select(r => r.AchievedAt).ToListAsync(cancellationToken));

        // MealEntry hangs off the diet plan rather than the member, and ConsumedAt is nullable — an
        // entry with no timestamp cannot be placed in the window, so it is not counted as being in it.
        var mealDays = DaysInWindow(await db.MealEntries.AsNoTracking()
            .Where(e => e.DietPlan!.MemberId == memberId && e.ConsumedAt != null)
            .Select(e => e.ConsumedAt!.Value)
            .ToListAsync(cancellationToken));

        // RecoveryLog stores a plain date, already in the gym's terms, so it needs no conversion.
        var recoveryDays = await db.RecoveryLogs.AsNoTracking()
            .CountAsync(r => r.MemberId == memberId && r.LoggedOn >= windowStart, cancellationToken);

        var inActiveChallenge = await db.ChallengeParticipants.AsNoTracking()
            .AnyAsync(
                p => p.MemberId == memberId && !p.IsCompleted
                     && p.Challenge!.StartDate <= today && p.Challenge.EndDate >= today,
                cancellationToken);

        var tips = RankClimbPolicy.TipsFor(new RankClimbPolicy.ClimbActivity(
            workouts, checkIns, personalRecords, mealDays, recoveryDays, inActiveChallenge));


        return new MyRankLadderDto(
            rungs,
            rungBoard,
            chasing,
            pace.XpPerWeek,
            pace.WeeksToNextTier,
            RankClimbPolicy.PaceWindowDays,
            workouts,
            xpByReason
                .OrderByDescending(kv => kv.Value)
                .Select(kv => new MyXpSourceDto(kv.Key.ToString(), kv.Value))
                .ToList(),
            tips.Select(t => new MyClimbTipDto(t.Code, t.Title, t.Detail, t.XpValue)).ToList());
    }
}
