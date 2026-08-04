using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Workouts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Workouts.Commands;

public record AssignWorkoutTemplateCommand(
    Guid MemberId, Guid WorkoutTemplateId, DateOnly StartDate, DateOnly? EndDate, string? Notes) : ICommand<Guid>;

public class AssignWorkoutTemplateCommandValidator : AbstractValidator<AssignWorkoutTemplateCommand>
{
    public AssignWorkoutTemplateCommandValidator()
    {
        RuleFor(x => x.MemberId).NotEmpty();
        RuleFor(x => x.WorkoutTemplateId).NotEmpty();
    }
}

public class AssignWorkoutTemplateCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<AssignWorkoutTemplateCommand, Guid>
{
    public async Task<Guid> Handle(AssignWorkoutTemplateCommand request, CancellationToken cancellationToken)
    {
        var memberExists = await db.Members.AnyAsync(m => m.Id == request.MemberId, cancellationToken);
        if (!memberExists)
        {
            throw new NotFoundException(nameof(Domain.Members.Member), request.MemberId);
        }

        var templateExists = await db.WorkoutTemplates.AnyAsync(t => t.Id == request.WorkoutTemplateId, cancellationToken);
        if (!templateExists)
        {
            throw new NotFoundException(nameof(WorkoutTemplate), request.WorkoutTemplateId);
        }

        var assignment = new WorkoutAssignment
        {
            MemberId = request.MemberId,
            WorkoutTemplateId = request.WorkoutTemplateId,
            AssignedByUserId = currentUser.UserId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Notes = request.Notes
        };

        db.WorkoutAssignments.Add(assignment);
        await db.SaveChangesAsync(cancellationToken);

        return assignment.Id;
    }
}
