using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Nutrition.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Nutrition.Queries;

public record GetMemberWaterLogsQuery(Guid MemberId) : IQuery<List<WaterLogDto>>;

public class GetMemberWaterLogsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetMemberWaterLogsQuery, List<WaterLogDto>>
{
    public Task<List<WaterLogDto>> Handle(GetMemberWaterLogsQuery request, CancellationToken cancellationToken)
        => db.WaterLogs.AsNoTracking()
            .Where(w => w.MemberId == request.MemberId)
            .OrderByDescending(w => w.LoggedAt)
            .Take(50)
            .Select(w => new WaterLogDto(w.Id, w.AmountMl, w.LoggedAt))
            .ToListAsync(cancellationToken);
}
