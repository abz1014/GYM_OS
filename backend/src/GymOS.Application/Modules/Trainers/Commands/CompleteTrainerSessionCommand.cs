using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Trainers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Trainers.Commands;

public record CompleteTrainerSessionCommand(Guid SessionId, string? Notes) : ICommand<Unit>;

public class CompleteTrainerSessionCommandValidator : AbstractValidator<CompleteTrainerSessionCommand>
{
    public CompleteTrainerSessionCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
    }
}

public class CompleteTrainerSessionCommandHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<CompleteTrainerSessionCommand, Unit>
{
    public async Task<Unit> Handle(CompleteTrainerSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await db.TrainerSessions.FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken)
            ?? throw new NotFoundException(nameof(TrainerSession), request.SessionId);

        if (session.Status != TrainerSessionStatus.Scheduled)
        {
            throw new ValidationException("Only a scheduled session can be completed.");
        }

        session.Status = TrainerSessionStatus.Completed;
        session.CompletedAt = dateTimeProvider.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            session.Notes = request.Notes;
        }

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
