using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Experience;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Experience.EventHandlers;

/// <summary>
/// Records the moment a member reaches a new rung, on every path that earns XP.
///
/// Subscribed to MemberProgressionChanged rather than to the workout logger, and that is the whole
/// point. The one celebration this system had fired only on POST /api/me/workouts, so a member who
/// levelled up by checking in at the front desk, logging a meal, or finishing a challenge got
/// nothing at all — the app stayed silent through the exact moments it should have spoken. Every one
/// of those raises this event.
///
/// WRITES EVERY MISSING RUNG, NOT JUST THE TOP ONE. A single challenge completion is worth 150 XP and
/// can carry somebody across two thresholds at once; recording only the highest would quietly delete
/// a rank they genuinely passed through, and the ladder on screen would show a gap it could not
/// explain. Backfilling this way also means a member whose XP predates this feature collects their
/// history on the next thing they do, rather than starting from an empty record that implies they
/// never climbed at all.
///
/// Idempotency is the unique index on (MemberId, Tier), not a flag: PeakXp ratchets, so a tier is
/// reached exactly once in a member's life. That makes running on every award safe by construction —
/// re-running finds nothing to insert.
/// </summary>
public class RecordRankPromotionsOnProgressionChangedHandler(
    IApplicationDbContext db, IDateTimeProvider dateTimeProvider)
    : INotificationHandler<DomainEventNotification<MemberProgressionChangedEvent>>
{
    public async Task Handle(
        DomainEventNotification<MemberProgressionChangedEvent> notification, CancellationToken cancellationToken)
    {
        var memberId = notification.DomainEvent.MemberId;

        var progression = await db.MemberProgressions.AsNoTracking()
            .Where(p => p.MemberId == memberId)
            .Select(p => new { p.TenantId, p.PeakXp })
            .FirstOrDefaultAsync(cancellationToken);

        if (progression is null)
        {
            return;
        }

        var earned = RankPolicy.TierFor(progression.PeakXp);

        // Newcomer is where everybody starts. Recording it would mean every member's history opens
        // with a promotion they were given for existing, which cheapens the ones they worked for.
        if (earned == RankTier.Newcomer)
        {
            return;
        }

        var already = await db.RankPromotions
            .Where(r => r.MemberId == memberId)
            .Select(r => r.Tier)
            .ToListAsync(cancellationToken);

        var now = dateTimeProvider.UtcNow;
        var added = false;

        for (var tier = RankTier.Regular; tier <= earned; tier++)
        {
            if (already.Contains(tier))
            {
                continue;
            }

            db.RankPromotions.Add(new RankPromotion
            {
                TenantId = progression.TenantId,
                MemberId = memberId,
                Tier = tier,
                AchievedAt = now,
                Seen = false
            });
            added = true;
        }

        if (added)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
