using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Nutrition.Dtos;
using GymOS.Application.Modules.Nutrition.Queries;
using MediatR;

namespace GymOS.Application.Modules.Portal.Queries;

public record GetMyWaterLogsQuery : IQuery<List<WaterLogDto>>;

public class GetMyWaterLogsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser, ISender sender)
    : IRequestHandler<GetMyWaterLogsQuery, List<WaterLogDto>>
{
    public async Task<List<WaterLogDto>> Handle(GetMyWaterLogsQuery request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);
        return await sender.Send(new GetMemberWaterLogsQuery(memberId), cancellationToken);
    }
}
