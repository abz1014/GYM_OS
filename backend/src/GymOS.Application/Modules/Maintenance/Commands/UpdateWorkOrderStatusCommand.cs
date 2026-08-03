using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Domain.Equipment;
using GymOS.Domain.Maintenance;
using GymOS.Application.Common.Messaging;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Maintenance.Commands;

public record UpdateWorkOrderStatusCommand(Guid WorkOrderId, WorkOrderStatus Status) : ICommand<Unit>;

public class UpdateWorkOrderStatusCommandValidator : AbstractValidator<UpdateWorkOrderStatusCommand>
{
    public UpdateWorkOrderStatusCommandValidator() => RuleFor(x => x.WorkOrderId).NotEmpty();
}

public class UpdateWorkOrderStatusCommandHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<UpdateWorkOrderStatusCommand, Unit>
{
    public async Task<Unit> Handle(UpdateWorkOrderStatusCommand request, CancellationToken cancellationToken)
    {
        if (request.Status == WorkOrderStatus.Completed)
        {
            throw new ValidationException("A work order can only be completed by verifying it — use the verify endpoint.");
        }

        var workOrder = await db.WorkOrders
            .Include(w => w.Asset)
            .Include(w => w.DowntimeLogs)
            .FirstOrDefaultAsync(w => w.Id == request.WorkOrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(WorkOrder), request.WorkOrderId);

        workOrder.Status = request.Status;

        if (request.Status == WorkOrderStatus.Cancelled)
        {
            var openDowntime = workOrder.DowntimeLogs.FirstOrDefault(d => d.EndedAt == null);
            if (openDowntime is not null)
            {
                openDowntime.EndedAt = dateTimeProvider.UtcNow;
            }

            if (workOrder.Asset is not null && workOrder.Asset.Status == AssetStatus.UnderMaintenance)
            {
                workOrder.Asset.Status = AssetStatus.Active;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
