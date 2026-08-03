using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Nutrition.Dtos;
using GymOS.Application.Modules.Nutrition.Queries;
using MediatR;

namespace GymOS.Application.Modules.Portal.Queries;

public record GetMyDietPlansQuery : IQuery<List<DietPlanListItemDto>>;

public class GetMyDietPlansQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser, ISender sender)
    : IRequestHandler<GetMyDietPlansQuery, List<DietPlanListItemDto>>
{
    public async Task<List<DietPlanListItemDto>> Handle(GetMyDietPlansQuery request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);
        return await sender.Send(new GetMemberDietPlansQuery(memberId), cancellationToken);
    }
}
