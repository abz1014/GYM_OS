using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Nutrition.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Nutrition.Queries;

public record GetMemberDietPlansQuery(Guid MemberId) : IQuery<List<DietPlanListItemDto>>;

public class GetMemberDietPlansQueryHandler(IApplicationDbContext db) : IRequestHandler<GetMemberDietPlansQuery, List<DietPlanListItemDto>>
{
    public Task<List<DietPlanListItemDto>> Handle(GetMemberDietPlansQuery request, CancellationToken cancellationToken)
        => db.DietPlans.AsNoTracking()
            .Where(p => p.MemberId == request.MemberId)
            .OrderByDescending(p => p.StartDate)
            .Select(p => new DietPlanListItemDto(p.Id, p.Name, p.TargetCalories, p.TargetProteinG, p.TargetCarbsG, p.TargetFatG, p.StartDate, p.EndDate))
            .ToListAsync(cancellationToken);
}
