using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Memberships.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Memberships.Queries;

public record GetDiscountsQuery(bool IncludeInactive = false) : IQuery<List<DiscountDto>>;

public class GetDiscountsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetDiscountsQuery, List<DiscountDto>>
{
    public Task<List<DiscountDto>> Handle(GetDiscountsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Discounts.AsNoTracking().AsQueryable();

        if (!request.IncludeInactive)
        {
            query = query.Where(d => d.IsActive);
        }

        return query
            .OrderBy(d => d.Name)
            .Select(d => new DiscountDto(d.Id, d.Name, d.Type, d.Value, d.MembershipPlanId, d.ValidFrom, d.ValidTo, d.IsActive))
            .ToListAsync(cancellationToken);
    }
}
