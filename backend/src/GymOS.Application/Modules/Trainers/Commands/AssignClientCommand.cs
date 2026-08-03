using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Trainers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Trainers.Commands;

public record AssignClientCommand(Guid TrainerId, Guid MemberId) : ICommand<Guid>;

public class AssignClientCommandValidator : AbstractValidator<AssignClientCommand>
{
    public AssignClientCommandValidator()
    {
        RuleFor(x => x.TrainerId).NotEmpty();
        RuleFor(x => x.MemberId).NotEmpty();
    }
}

public class AssignClientCommandHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<AssignClientCommand, Guid>
{
    public async Task<Guid> Handle(AssignClientCommand request, CancellationToken cancellationToken)
    {
        var trainerExists = await db.Trainers.AnyAsync(t => t.Id == request.TrainerId, cancellationToken);
        if (!trainerExists)
        {
            throw new NotFoundException(nameof(Trainer), request.TrainerId);
        }

        var memberExists = await db.Members.AnyAsync(m => m.Id == request.MemberId, cancellationToken);
        if (!memberExists)
        {
            throw new NotFoundException(nameof(Domain.Members.Member), request.MemberId);
        }

        var assignment = new TrainerAssignment
        {
            TrainerId = request.TrainerId,
            MemberId = request.MemberId,
            StartDate = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime),
            IsActive = true
        };

        db.TrainerAssignments.Add(assignment);
        await db.SaveChangesAsync(cancellationToken);

        return assignment.Id;
    }
}
