using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Common.Security;
using GymOS.Application.Modules.Maintenance.Dtos;
using GymOS.Domain.Maintenance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Maintenance.Queries;

public record GetWorkOrdersListQuery(Guid? BranchId, WorkOrderStatus? Status) : IQuery<List<WorkOrderListItemDto>>;

public class GetWorkOrdersListQueryHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider, ICurrentUserService currentUser)
    : IRequestHandler<GetWorkOrdersListQuery, List<WorkOrderListItemDto>>
{
    public async Task<List<WorkOrderListItemDto>> Handle(GetWorkOrdersListQuery request, CancellationToken cancellationToken)
    {
        var accessibleBranchIds = await BranchAccessResolver.GetAccessibleBranchIdsAsync(db, currentUser, cancellationToken);
        var query = db.WorkOrders.AsNoTracking().Include(w => w.Asset).Where(w => accessibleBranchIds.Contains(w.BranchId));

        if (request.BranchId is not null)
        {
            query = query.Where(w => w.BranchId == request.BranchId);
        }

        if (request.Status is not null)
        {
            query = query.Where(w => w.Status == request.Status);
        }

        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);

        return await query
            .OrderBy(w => w.ScheduledDate)
            .Select(w => new WorkOrderListItemDto(
                w.Id, w.Asset!.Name, w.Asset.AssetTag, w.Type, w.Priority, w.Status, w.Title, w.ScheduledDate,
                w.ScheduledDate != null && w.ScheduledDate < today && w.Status != WorkOrderStatus.Completed && w.Status != WorkOrderStatus.Cancelled))
            .ToListAsync(cancellationToken);
    }
}
