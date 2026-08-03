using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Maintenance.Dtos;
using GymOS.Domain.Maintenance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Maintenance.Queries;

public record GetWorkOrderByIdQuery(Guid Id) : IQuery<WorkOrderDetailDto>;

public class GetWorkOrderByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetWorkOrderByIdQuery, WorkOrderDetailDto>
{
    public async Task<WorkOrderDetailDto> Handle(GetWorkOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var workOrder = await db.WorkOrders.AsNoTracking()
            .Include(w => w.Asset)
            .Include(w => w.DowntimeLogs)
            .FirstOrDefaultAsync(w => w.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(WorkOrder), request.Id);

        return new WorkOrderDetailDto(
            workOrder.Id, workOrder.AssetId, workOrder.Asset?.Name ?? string.Empty, workOrder.Asset?.AssetTag ?? string.Empty,
            workOrder.Type, workOrder.Priority, workOrder.Status, workOrder.Title, workOrder.Description,
            workOrder.AssignedToUserId, workOrder.ScheduledDate, workOrder.CompletedDate, workOrder.Cost,
            workOrder.DowntimeLogs.Select(d => new DowntimeLogDto(d.Id, d.StartedAt, d.EndedAt, d.Reason)).ToList());
    }
}
