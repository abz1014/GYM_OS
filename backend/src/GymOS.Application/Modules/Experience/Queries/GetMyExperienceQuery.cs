using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Experience.Dtos;
using GymOS.Application.Modules.Portal;
using GymOS.Domain.Experience;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Experience.Queries;

/// <summary>
/// The member's level/XP card in one round trip. Reads the MemberProgression projection (defaulting
/// to a clean level 1 for a member who hasn't earned anything yet) plus the most recent ledger
/// entries. Identity via MyMemberResolver — self-scoped like the rest of /api/me, no id parameter.
/// </summary>
public record GetMyExperienceQuery : IQuery<MyExperienceDto>;

public class GetMyExperienceQueryHandler(
    IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetMyExperienceQuery, MyExperienceDto>
{
    private const int RecentCount = 8;

    public async Task<MyExperienceDto> Handle(GetMyExperienceQuery request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);

        // PeakXp, not TotalXp. The two differ only after an undo, and this is the member-facing
        // number: a mis-tap corrected within the undo window must not visibly cost someone a level
        // they had already reached. TotalXp remains the ledger's honest sum for anything auditing it.
        var totalXp = await db.MemberProgressions.AsNoTracking()
            .Where(p => p.MemberId == memberId)
            .Select(p => (long?)p.PeakXp)
            .FirstOrDefaultAsync(cancellationToken) ?? 0L;

        var (level, xpIntoLevel, xpForNextLevel) = XpPolicy.LevelForXp(totalXp);

        // OccurredAt is a DateTimeOffset — SQLite (the in-memory test provider) can't ORDER BY it in
        // SQL, so pull this member's rows and order/take in memory (same pattern as the leads and
        // attendance queries). A member's ledger is small and member-scoped.
        var all = await db.XpTransactions.AsNoTracking()
            .Where(t => t.MemberId == memberId)
            .Select(t => new { t.Amount, t.Reason, t.OccurredAt })
            .ToListAsync(cancellationToken);

        var recent = all
            .OrderByDescending(t => t.OccurredAt)
            .Take(RecentCount)
            .Select(t => new MyXpEntryDto(t.Amount, t.Reason.ToString(), t.OccurredAt))
            .ToList();

        /*
         * Days since the member was last seen, for the demotion side of the ladder.
         *
         * "Seen" is the LATEST of a logged workout and a gym visit, not workouts alone. A member who
         * comes in three times a week and never opens the logger is training, and telling them they
         * had slipped would be the app calling them absent for not doing paperwork.
         *
         * Read off the XP ledger rather than joining WorkoutLogs and AttendanceRecords, because every
         * one of those actions already writes a transaction here — so this is one list already in
         * memory, and it stays correct automatically if a new earning action is added later.
         */
        var lastEarnedAt = all.Count == 0 ? (DateTimeOffset?)null : all.Max(t => t.OccurredAt);
        var daysSinceLastActivity = lastEarnedAt is null
            ? (int?)null
            : Math.Max(0, (int)(dateTimeProvider.UtcNow - lastEarnedAt.Value).TotalDays);

        var standing = RankPolicy.StandingFor(totalXp, daysSinceLastActivity);
        var (intoTier, tierSpan) = RankPolicy.ProgressWithin(totalXp);
        var nextTier = RankPolicy.NextTierAfter(standing.Peak);

        // How long until absence costs another rung. Null inside the grace period and for a member who
        // has never trained — a countdown to a punishment nobody is facing is just an alarming number.
        int? daysUntilNextDemotion = null;
        if (daysSinceLastActivity is int away && away > RankPolicy.GraceDays && standing.Current != RankTier.Newcomer)
        {
            var beyondGrace = away - RankPolicy.GraceDays;
            daysUntilNextDemotion = RankPolicy.DaysPerTierLost - (beyondGrace % RankPolicy.DaysPerTierLost);
        }

        /*
         * The climb, and whether the top of it has been shown yet.
         *
         * Unseen is the HIGHEST unseen rung, not the oldest and not a list. A member who earned two
         * rungs from one challenge should be congratulated on where they arrived, not walked through
         * an interstitial per rung — and the ones below it are still in Promotions, so nothing is
         * hidden, it simply is not queued up as a sequence of interruptions.
         */
        var promotionRows = await db.RankPromotions.AsNoTracking()
            .Where(r => r.MemberId == memberId)
            .Select(r => new { r.Id, r.Tier, r.AchievedAt, r.Seen })
            .ToListAsync(cancellationToken);

        var promotions = promotionRows
            .OrderByDescending(r => r.Tier)
            .Select(r => new MyRankPromotionDto(r.Id, r.Tier.ToString(), r.AchievedAt, r.Seen))
            .ToList();

        var unseen = promotionRows
            .Where(r => !r.Seen)
            .OrderByDescending(r => r.Tier)
            .Select(r => new MyRankPromotionDto(r.Id, r.Tier.ToString(), r.AchievedAt, r.Seen))
            .FirstOrDefault();

        var rank = new MyRankDto(
            standing.Peak.ToString(),
            standing.Current.ToString(),
            standing.TiersLostToAbsence,
            nextTier?.ToString(),
            intoTier,
            tierSpan,
            nextTier is null ? 0 : Math.Max(0, RankPolicy.XpRequiredFor(nextTier.Value) - totalXp),
            daysUntilNextDemotion,
            promotions,
            unseen);

        return new MyExperienceDto(level, totalXp, xpIntoLevel, xpForNextLevel, recent, rank);
    }
}
