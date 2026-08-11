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

public class GetMyExperienceQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
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

        return new MyExperienceDto(level, totalXp, xpIntoLevel, xpForNextLevel, recent);
    }
}
