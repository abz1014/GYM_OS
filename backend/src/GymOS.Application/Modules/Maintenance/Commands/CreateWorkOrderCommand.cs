using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Equipment;
using GymOS.Domain.Maintenance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Maintenance.Commands;

public record CreateWorkOrderCommand(
    Guid AssetId, WorkOrderType Type, WorkOrderPriority Priority, string Title, string? Description,
    Guid? AssignedToUserId, DateOnly? ScheduledDate) : ICommand<Guid>;

public class CreateWorkOrderCommandValidator : AbstractValidator<CreateWorkOrderCommand>
{
    public CreateWorkOrderCommandValidator()
    {
        RuleFor(x => x.AssetId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
    }
}

public class CreateWorkOrderCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<CreateWorkOrderCommand, Guid>
{
    public async Task<Guid> Handle(CreateWorkOrderCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new ForbiddenAccessException("No tenant context.");

        var asset = await db.Assets.FirstOrDefaultAsync(a => a.Id == request.AssetId, cancellationToken)
            ?? throw new NotFoundException(nameof(Asset), request.AssetId);

        var workOrder = new WorkOrder
        {
            TenantId = tenantId,
            BranchId = asset.BranchId,
            AssetId = request.AssetId,
            Type = request.Type,
            Priority = request.Priority,
            Status = WorkOrderStatus.Open,
            Title = request.Title,
            Description = request.Description,
            AssignedToUserId = request.AssignedToUserId,
            ScheduledDate = request.ScheduledDate
        };

        db.WorkOrders.Add(workOrder);

        if (request.Type == WorkOrderType.Corrective)
        {
            asset.Status = AssetStatus.UnderMaintenance;
            db.DowntimeLogs.Add(new DowntimeLog
            {
                AssetId = asset.Id,
                WorkOrder = workOrder,
                StartedAt = DateTimeOffset.UtcNow,
                Reason = request.Title
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        return workOrder.Id;
    }
}
