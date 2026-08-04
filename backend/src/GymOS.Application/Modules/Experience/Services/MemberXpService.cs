using GymOS.Application.Common.Interfaces;
using GymOS.Domain.Experience;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Experience.Services;

/// <summary>
/// The single write-path into the experience ledger, shared by every domain-event handler that
/// awards XP. Keeps the award idempotent and the level projection in lock-step with the ledger, so
/// no handler has to re-implement either. Does NOT call SaveChanges — it only mutates the tracked
/// context; GymOsDbContext persists the changes when it finishes dispatching domain events (inside
/// the same ambient transaction the command opened).
/// </summary>
public interface IMemberXpService
{
    Task AwardAsync(Guid memberId, XpReason reason, XpSourceType sourceType, Guid? sourceId, CancellationToken cancellationToken);
}

public class MemberXpService(IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IMemberXpService
{
    public async Task AwardAsync(Guid memberId, XpReason reason, XpSourceType sourceType, Guid? sourceId, CancellationToken cancellationToken)
    {
        // Events are always raised from an authenticated command, so the tenant is on the JWT; fall
        // back to the member's own tenant so a future out-of-request caller (e.g. a rebuild job) still
        // works. No tenant at all -> nothing to scope the ledger to, so skip.
        var tenantId = currentUser.TenantId
            ?? await db.Members.IgnoreQueryFilters().Where(m => m.Id == memberId).Select(m => (Guid?)m.TenantId)
                .FirstOrDefaultAsync(cancellationToken);
        if (tenantId is null)
        {
            return;
        }

        // Idempotency: the same (member, source, reason) is never credited twice, so a re-published
        // event or a retry can't double-award.
        var alreadyAwarded = await db.XpTransactions
            .AnyAsync(t => t.MemberId == memberId && t.SourceType == sourceType && t.SourceId == sourceId && t.Reason == reason,
                cancellationToken);
        if (alreadyAwarded)
        {
            return;
        }

        var amount = XpPolicy.AwardFor(reason);
        if (amount <= 0)
        {
            return;
        }

        var now = dateTimeProvider.UtcNow;

        db.XpTransactions.Add(new XpTransaction
        {
            TenantId = tenantId.Value,
            MemberId = memberId,
            Amount = amount,
            Reason = reason,
            SourceType = sourceType,
            SourceId = sourceId,
            OccurredAt = now
        });

        var progression = await db.MemberProgressions.FirstOrDefaultAsync(p => p.MemberId == memberId, cancellationToken);
        if (progression is null)
        {
            progression = new MemberProgression { TenantId = tenantId.Value, MemberId = memberId };
            db.MemberProgressions.Add(progression);
        }

        progression.AddXp(amount);
        progression.UpdatedAt = now;
    }
}
