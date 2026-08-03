using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Trainers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Trainers.Commands;

public record ScheduleTrainerSessionCommand(
    Guid TrainerAssignmentId, DateTimeOffset ScheduledAt, int DurationMinutes, string? Notes) : ICommand<Guid>;

public class ScheduleTrainerSessionCommandValidator : AbstractValidator<ScheduleTrainerSessionCommand>
{
    public ScheduleTrainerSessionCommandValidator()
    {
        RuleFor(x => x.TrainerAssignmentId).NotEmpty();
        RuleFor(x => x.DurationMinutes).InclusiveBetween(15, 240);
    }
}

public class ScheduleTrainerSessionCommandHandler(IApplicationDbContext db)
    : IRequestHandler<ScheduleTrainerSessionCommand, Guid>
{
    public async Task<Guid> Handle(ScheduleTrainerSessionCommand request, CancellationToken cancellationToken)
    {
        var assignment = await db.TrainerAssignments
            .FirstOrDefaultAsync(a => a.Id == request.TrainerAssignmentId, cancellationToken)
            ?? throw new NotFoundException(nameof(TrainerAssignment), request.TrainerAssignmentId);

        if (!assignment.IsActive)
        {
            throw new ValidationException("Cannot schedule a session for an ended assignment.");
        }

        var session = new TrainerSession
        {
            TrainerAssignmentId = assignment.Id,
            ScheduledAt = request.ScheduledAt,
            DurationMinutes = request.DurationMinutes,
            Notes = request.Notes,
            Status = TrainerSessionStatus.Scheduled
        };

        db.TrainerSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        return session.Id;
    }
}
