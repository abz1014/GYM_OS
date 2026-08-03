using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Crm;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Crm.Commands;

/// <summary>The workflow's missing "Completion" step for a follow-up task — until this existed,
/// CompletedAt existed on the entity but nothing ever set it, so a due activity stayed pending
/// (and its one-time reminder fired) forever with no way to resolve it.</summary>
public record CompleteLeadActivityCommand(Guid LeadActivityId) : ICommand<Unit>;

public class CompleteLeadActivityCommandValidator : AbstractValidator<CompleteLeadActivityCommand>
{
    public CompleteLeadActivityCommandValidator() => RuleFor(x => x.LeadActivityId).NotEmpty();
}

public class CompleteLeadActivityCommandHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<CompleteLeadActivityCommand, Unit>
{
    public async Task<Unit> Handle(CompleteLeadActivityCommand request, CancellationToken cancellationToken)
    {
        var activity = await db.LeadActivities.FirstOrDefaultAsync(a => a.Id == request.LeadActivityId, cancellationToken)
            ?? throw new NotFoundException(nameof(LeadActivity), request.LeadActivityId);

        if (activity.CompletedAt is not null)
        {
            throw new ValidationException("This activity is already complete.");
        }

        activity.CompletedAt = dateTimeProvider.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
