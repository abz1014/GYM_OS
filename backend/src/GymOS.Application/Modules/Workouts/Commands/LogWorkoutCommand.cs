using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Workouts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Workouts.Commands;

public record WorkoutLogEntryInput(Guid ExerciseId, int SetsCompleted, int RepsCompleted, decimal? WeightKg);

public record LogWorkoutCommand(Guid MemberId, Guid? WorkoutTemplateId, IReadOnlyList<WorkoutLogEntryInput> Entries) : ICommand<Guid>;

public class LogWorkoutCommandValidator : AbstractValidator<LogWorkoutCommand>
{
    public LogWorkoutCommandValidator()
    {
        RuleFor(x => x.MemberId).NotEmpty();
        RuleFor(x => x.Entries).NotEmpty();
    }
}

public class LogWorkoutCommandHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<LogWorkoutCommand, Guid>
{
    public async Task<Guid> Handle(LogWorkoutCommand request, CancellationToken cancellationToken)
    {
        var memberExists = await db.Members.AnyAsync(m => m.Id == request.MemberId, cancellationToken);
        if (!memberExists)
        {
            throw new NotFoundException(nameof(Domain.Members.Member), request.MemberId);
        }

        var log = new WorkoutLog
        {
            MemberId = request.MemberId,
            WorkoutTemplateId = request.WorkoutTemplateId,
            LoggedAt = dateTimeProvider.UtcNow
        };

        foreach (var entry in request.Entries)
        {
            log.Entries.Add(new WorkoutLogEntry
            {
                ExerciseId = entry.ExerciseId,
                SetsCompleted = entry.SetsCompleted,
                RepsCompleted = entry.RepsCompleted,
                WeightKg = entry.WeightKg
            });
        }

        db.WorkoutLogs.Add(log);
        await db.SaveChangesAsync(cancellationToken);

        return log.Id;
    }
}
