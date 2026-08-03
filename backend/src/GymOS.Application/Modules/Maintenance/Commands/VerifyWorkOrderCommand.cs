using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Equipment;
using GymOS.Domain.Maintenance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Maintenance.Commands;

/// <summary>
/// Closes out the Repair step: a work order pending verification is either approved (completed —
/// asset restored, downtime closed, and any linked recurring schedule advanced to its next due date)
/// or rejected (sent back for more repair work).
/// </summary>
public record VerifyWorkOrderCommand(
    Guid WorkOrderId, bool Approved, decimal? Cost, string? Notes, DateOnly? NextDueDate) : ICommand<Unit>;

public class VerifyWorkOrderCommandValidator : AbstractValidator<VerifyWorkOrderCommand>
{
    public VerifyWorkOrderCommandValidator() => RuleFor(x => x.WorkOrderId).NotEmpty();
}

public class VerifyWorkOrderCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<VerifyWorkOrderCommand, Unit>
{
    public async Task<Unit> Handle(VerifyWorkOrderCommand request, CancellationToken cancellationToken)
    {
        var workOrder = await db.WorkOrders
            .Include(w => w.Asset)
            .Include(w => w.DowntimeLogs)
            .FirstOrDefaultAsync(w => w.Id == request.WorkOrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(WorkOrder), request.WorkOrderId);

        if (workOrder.Status != WorkOrderStatus.PendingVerification)
        {
            throw new ValidationException("Only a work order pending verification can be verified.");
        }

        workOrder.VerificationNotes = request.Notes;
        workOrder.VerifiedByUserId = currentUser.UserId;
        workOrder.VerifiedAt = dateTimeProvider.UtcNow;

        if (!request.Approved)
        {
            workOrder.Status = WorkOrderStatus.InProgress;
            await db.SaveChangesAsync(cancellationToken);
            return Unit.Value;
        }

        if (workOrder.MaintenanceScheduleId is not null && request.NextDueDate is null)
        {
            throw new ValidationException("This work order is linked to a recurring schedule — provide its next due date.");
        }

        workOrder.Status = WorkOrderStatus.Completed;
        workOrder.CompletedDate = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);
        workOrder.Cost = request.Cost;

        var openDowntime = workOrder.DowntimeLogs.FirstOrDefault(d => d.EndedAt == null);
        if (openDowntime is not null)
        {
            openDowntime.EndedAt = dateTimeProvider.UtcNow;
        }

        if (workOrder.Asset is not null && workOrder.Asset.Status == AssetStatus.UnderMaintenance)
        {
            workOrder.Asset.Status = AssetStatus.Active;
        }

        if (workOrder.MaintenanceScheduleId is not null)
        {
            var schedule = await db.MaintenanceSchedules.FirstOrDefaultAsync(
                s => s.Id == workOrder.MaintenanceScheduleId, cancellationToken)
                ?? throw new NotFoundException(nameof(MaintenanceSchedule), workOrder.MaintenanceScheduleId.Value);

            schedule.NextDueDate = request.NextDueDate!.Value;
        }

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
