using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Trainers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Trainers.Commands;

/// <summary>
/// Edits a trainer after hiring. CreateTrainerCommand existed from the start and this did not, so a
/// trainer's specialties, commission and bio were fixed at the moment they were typed — a coach who
/// picked up a new certification, or negotiated a different rate, could not be updated at all.
///
/// Branch is not editable here. A trainer's branch decides which schedules, assignments and sessions
/// are theirs, and moving it would strand that history against a site they no longer work at; branch
/// moves belong with the staff account, not with the coaching profile.
/// </summary>
public record UpdateTrainerCommand(Guid Id, string Specialties, decimal CommissionRate, string? Bio, bool IsActive)
    : ICommand<Unit>;

public class UpdateTrainerCommandValidator : AbstractValidator<UpdateTrainerCommand>
{
    public UpdateTrainerCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.CommissionRate).InclusiveBetween(0, 100);
    }
}

public class UpdateTrainerCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateTrainerCommand, Unit>
{
    public async Task<Unit> Handle(UpdateTrainerCommand request, CancellationToken cancellationToken)
    {
        var trainer = await db.Trainers.FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Trainer), request.Id);

        trainer.Specialties = request.Specialties;
        trainer.CommissionRate = request.CommissionRate;
        trainer.Bio = request.Bio;

        var wasActive = trainer.IsActive;
        trainer.IsActive = request.IsActive;

        /*
         * Standing a trainer down also ends their login, because otherwise it doesn't end anything.
         *
         * Trainer.IsActive only removes them from assignment pickers; the User row behind them is a
         * separate record, and leaving it active means a coach who no longer works here keeps signing
         * in and reading every member's file. Employment and access are the same event.
         *
         * The reverse is not symmetric on purpose. Re-activating the coaching profile does not switch
         * a login back on, because the login may have been deactivated for its own reason — that is a
         * decision for the staff screen, which is now the one place accounts are switched on.
         */
        if (wasActive && !request.IsActive)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == trainer.UserId, cancellationToken);
            if (user is not null)
            {
                user.IsActive = false;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
