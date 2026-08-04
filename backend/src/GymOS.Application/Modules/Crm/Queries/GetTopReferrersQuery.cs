using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Common.Security;
using GymOS.Application.Modules.Crm.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Crm.Queries;

/// <summary>
/// Which members bring in the most new members. Lives in CRM because referrals are an acquisition
/// channel — this is the list a manager works through when deciding who deserves a thank-you or a
/// referral reward. Counts referred members in branches the caller can access (same
/// BranchAccessResolver rule as the pipeline summary); referrer names resolve tenant-wide since a
/// referrer can belong to a different branch than the person they brought in.
/// </summary>
public record GetTopReferrersQuery(Guid? BranchId, int Limit = 5) : IQuery<List<TopReferrerDto>>;

public class GetTopReferrersQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetTopReferrersQuery, List<TopReferrerDto>>
{
    public async Task<List<TopReferrerDto>> Handle(GetTopReferrersQuery request, CancellationToken cancellationToken)
    {
        var accessibleBranchIds = await BranchAccessResolver.GetAccessibleBranchIdsAsync(db, currentUser, cancellationToken);
        var referred = db.Members.AsNoTracking()
            .Where(m => m.ReferredByMemberId != null && accessibleBranchIds.Contains(m.BranchId));
        if (request.BranchId is not null)
        {
            referred = referred.Where(m => m.BranchId == request.BranchId);
        }

        var counts = await referred
            .GroupBy(m => m.ReferredByMemberId!.Value)
            .Select(g => new { ReferrerId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(request.Limit)
            .ToListAsync(cancellationToken);

        var referrerIds = counts.Select(c => c.ReferrerId).ToList();
        var referrers = await db.Members.AsNoTracking()
            .Where(m => referrerIds.Contains(m.Id))
            .Select(m => new { m.Id, m.FirstName, m.LastName, m.MemberCode })
            .ToListAsync(cancellationToken);

        return counts
            .Select(c =>
            {
                var referrer = referrers.FirstOrDefault(r => r.Id == c.ReferrerId);
                return referrer is null
                    ? null // referrer soft-deleted — their referees remain but the leaderboard row is gone
                    : new TopReferrerDto(referrer.Id, $"{referrer.FirstName} {referrer.LastName}", referrer.MemberCode, c.Count);
            })
            .Where(dto => dto is not null)
            .Select(dto => dto!)
            .ToList();
    }
}
