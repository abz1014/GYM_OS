using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Trainers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Trainers.Commands;

public record CancelTrainerSessionCommand(Guid SessionId) : ICommand<Unit>;

public class CancelTrainerSessionCommandValidator : AbstractValidator<CancelTrainerSessionCommand>
{
    public CancelTrainerSessionCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
    }
}

public class CancelTrainerSessionCommandHandler(IApplicationDbContext db)
    : IRequestHandler<CancelTrainerSessionCommand, Unit>
{
    public async Task<Unit> Handle(CancelTrainerSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await db.TrainerSessions.FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken)
            ?? throw new NotFoundException(nameof(TrainerSession), request.SessionId);

        if (session.Status != TrainerSessionStatus.Scheduled)
        {
            throw new ValidationException("Only a scheduled session can be cancelled.");
        }

        session.Status = TrainerSessionStatus.Cancelled;

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
