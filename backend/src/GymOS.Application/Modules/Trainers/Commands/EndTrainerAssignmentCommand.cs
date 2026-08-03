using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Trainers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Trainers.Commands;

public record EndTrainerAssignmentCommand(Guid AssignmentId) : ICommand<Unit>;

public class EndTrainerAssignmentCommandValidator : AbstractValidator<EndTrainerAssignmentCommand>
{
    public EndTrainerAssignmentCommandValidator()
    {
        RuleFor(x => x.AssignmentId).NotEmpty();
    }
}

public class EndTrainerAssignmentCommandHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<EndTrainerAssignmentCommand, Unit>
{
    public async Task<Unit> Handle(EndTrainerAssignmentCommand request, CancellationToken cancellationToken)
    {
        var assignment = await db.TrainerAssignments.FirstOrDefaultAsync(a => a.Id == request.AssignmentId, cancellationToken)
            ?? throw new NotFoundException(nameof(TrainerAssignment), request.AssignmentId);

        if (!assignment.IsActive)
        {
            throw new ValidationException("This assignment has already ended.");
        }

        assignment.IsActive = false;
        assignment.EndDate = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
