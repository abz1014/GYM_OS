using System.Globalization;
using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Trainers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Trainers.Commands;

public record CreateTrainerScheduleCommand(Guid TrainerId, DayOfWeek DayOfWeek, string StartTime, string EndTime, bool IsAvailable)
    : ICommand<Guid>;

public class CreateTrainerScheduleCommandValidator : AbstractValidator<CreateTrainerScheduleCommand>
{
    public CreateTrainerScheduleCommandValidator()
    {
        RuleFor(x => x.TrainerId).NotEmpty();
        RuleFor(x => x.StartTime).NotEmpty();
        RuleFor(x => x.EndTime).NotEmpty();
    }
}

public class CreateTrainerScheduleCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateTrainerScheduleCommand, Guid>
{
    public async Task<Guid> Handle(CreateTrainerScheduleCommand request, CancellationToken cancellationToken)
    {
        var trainerExists = await db.Trainers.AnyAsync(t => t.Id == request.TrainerId, cancellationToken);
        if (!trainerExists)
        {
            throw new NotFoundException(nameof(Trainer), request.TrainerId);
        }

        var startTime = TimeOnly.Parse(request.StartTime, CultureInfo.InvariantCulture);
        var endTime = TimeOnly.Parse(request.EndTime, CultureInfo.InvariantCulture);

        if (endTime <= startTime)
        {
            throw new ValidationException("End time must be after start time.");
        }

        var schedule = new TrainerSchedule
        {
            TrainerId = request.TrainerId,
            DayOfWeek = request.DayOfWeek,
            StartTime = startTime,
            EndTime = endTime,
            IsAvailable = request.IsAvailable
        };

        db.TrainerSchedules.Add(schedule);
        await db.SaveChangesAsync(cancellationToken);

        return schedule.Id;
    }
}
