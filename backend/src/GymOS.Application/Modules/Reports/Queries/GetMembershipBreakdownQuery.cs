using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Reports.Dtos;
using GymOS.Domain.Members;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Reports.Queries;

public record GetMembershipBreakdownQuery : IQuery<MembershipBreakdownDto>;

public class GetMembershipBreakdownQueryHandler(IApplicationDbContext db) : IRequestHandler<GetMembershipBreakdownQuery, MembershipBreakdownDto>
{
    public async Task<MembershipBreakdownDto> Handle(GetMembershipBreakdownQuery request, CancellationToken cancellationToken)
        => await BuildAsync(db, cancellationToken);

    internal static async Task<MembershipBreakdownDto> BuildAsync(IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var byStatus = await db.Members.AsNoTracking()
            .GroupBy(m => m.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var byPlanType = await db.MemberMemberships.AsNoTracking()
            .Where(mm => mm.Status == MemberMembershipStatus.Active)
            .GroupBy(mm => mm.MembershipPlan!.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return new MembershipBreakdownDto(
            byStatus.ToDictionary(x => x.Status.ToString(), x => x.Count),
            byPlanType.ToDictionary(x => x.Type.ToString(), x => x.Count));
    }
}
