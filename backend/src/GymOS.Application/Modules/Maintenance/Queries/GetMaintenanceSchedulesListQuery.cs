using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Maintenance.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Maintenance.Queries;

public record GetMaintenanceSchedulesListQuery(Guid? BranchId) : IQuery<List<MaintenanceScheduleDto>>;

public class GetMaintenanceSchedulesListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetMaintenanceSchedulesListQuery, List<MaintenanceScheduleDto>>
{
    public Task<List<MaintenanceScheduleDto>> Handle(GetMaintenanceSchedulesListQuery request, CancellationToken cancellationToken)
    {
        var query = db.MaintenanceSchedules.AsNoTracking().Include(s => s.Asset).AsQueryable();

        if (request.BranchId is not null)
        {
            query = query.Where(s => s.Asset!.BranchId == request.BranchId);
        }

        return query
            .OrderBy(s => s.NextDueDate)
            .Select(s => new MaintenanceScheduleDto(s.Id, s.AssetId, s.Asset!.Name, s.RecurrenceRule, s.NextDueDate, s.IsActive))
            .ToListAsync(cancellationToken);
    }
}
