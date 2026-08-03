using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Equipment;
using GymOS.Domain.Maintenance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Maintenance.Commands;

public record CreateMaintenanceScheduleCommand(Guid AssetId, string RecurrenceRule, DateOnly NextDueDate) : ICommand<Guid>;

public class CreateMaintenanceScheduleCommandValidator : AbstractValidator<CreateMaintenanceScheduleCommand>
{
    public CreateMaintenanceScheduleCommandValidator()
    {
        RuleFor(x => x.AssetId).NotEmpty();
        RuleFor(x => x.RecurrenceRule).NotEmpty();
    }
}

public class CreateMaintenanceScheduleCommandHandler(IApplicationDbContext db)
    : IRequestHandler<CreateMaintenanceScheduleCommand, Guid>
{
    public async Task<Guid> Handle(CreateMaintenanceScheduleCommand request, CancellationToken cancellationToken)
    {
        var assetExists = await db.Assets.AnyAsync(a => a.Id == request.AssetId, cancellationToken);
        if (!assetExists)
        {
            throw new NotFoundException(nameof(Asset), request.AssetId);
        }

        var schedule = new MaintenanceSchedule
        {
            AssetId = request.AssetId,
            RecurrenceRule = request.RecurrenceRule,
            NextDueDate = request.NextDueDate,
            IsActive = true
        };

        db.MaintenanceSchedules.Add(schedule);
        await db.SaveChangesAsync(cancellationToken);

        return schedule.Id;
    }
}
