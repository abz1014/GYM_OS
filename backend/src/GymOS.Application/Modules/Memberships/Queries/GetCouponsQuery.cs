using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Memberships.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Memberships.Queries;

public record GetCouponsQuery(bool IncludeInactive = false) : IQuery<List<CouponDto>>;

public class GetCouponsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetCouponsQuery, List<CouponDto>>
{
    public Task<List<CouponDto>> Handle(GetCouponsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Coupons.AsNoTracking().AsQueryable();

        if (!request.IncludeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        return query
            .OrderBy(c => c.Code)
            .Select(c => new CouponDto(c.Id, c.Code, c.DiscountId, c.MaxRedemptions, c.TimesRedeemed, c.ValidFrom, c.ValidTo, c.IsActive))
            .ToListAsync(cancellationToken);
    }
}
