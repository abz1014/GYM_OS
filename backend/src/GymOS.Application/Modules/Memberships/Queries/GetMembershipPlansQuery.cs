using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Memberships.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Memberships.Queries;

public record GetMembershipPlansQuery(bool IncludeInactive = false) : IQuery<List<MembershipPlanDto>>;

public class GetMembershipPlansQueryHandler(IApplicationDbContext db) : IRequestHandler<GetMembershipPlansQuery, List<MembershipPlanDto>>
{
    public Task<List<MembershipPlanDto>> Handle(GetMembershipPlansQuery request, CancellationToken cancellationToken)
    {
        var query = db.MembershipPlans.AsNoTracking().AsQueryable();

        if (!request.IncludeInactive)
        {
            query = query.Where(p => p.IsActive);
        }

        return query
            .OrderBy(p => p.Name)
            .Select(p => new MembershipPlanDto(
                p.Id, p.Name, p.Type, p.Description, p.DurationDays, p.Price, p.Currency, p.MaxFreezeDays, p.IsActive))
            .ToListAsync(cancellationToken);
    }
}
