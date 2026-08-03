using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Common.Security;
using GymOS.Application.Modules.Maintenance.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Maintenance.Queries;

public record GetMaintenanceSchedulesListQuery(Guid? BranchId) : IQuery<List<MaintenanceScheduleDto>>;

public class GetMaintenanceSchedulesListQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetMaintenanceSchedulesListQuery, List<MaintenanceScheduleDto>>
{
    public async Task<List<MaintenanceScheduleDto>> Handle(GetMaintenanceSchedulesListQuery request, CancellationToken cancellationToken)
    {
        var accessibleBranchIds = await BranchAccessResolver.GetAccessibleBranchIdsAsync(db, currentUser, cancellationToken);
        var query = db.MaintenanceSchedules.AsNoTracking().Include(s => s.Asset)
            .Where(s => accessibleBranchIds.Contains(s.Asset!.BranchId));

        if (request.BranchId is not null)
        {
            query = query.Where(s => s.Asset!.BranchId == request.BranchId);
        }

        return await query
            .OrderBy(s => s.NextDueDate)
            .Select(s => new MaintenanceScheduleDto(s.Id, s.AssetId, s.Asset!.Name, s.RecurrenceRule, s.NextDueDate, s.IsActive))
            .ToListAsync(cancellationToken);
    }
}
