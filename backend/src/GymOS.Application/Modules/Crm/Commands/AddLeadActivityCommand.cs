using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Crm;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Crm.Commands;

public record AddLeadActivityCommand(Guid LeadId, LeadActivityType Type, string Notes, DateTimeOffset? DueDate) : ICommand<Guid>;

public class AddLeadActivityCommandValidator : AbstractValidator<AddLeadActivityCommand>
{
    public AddLeadActivityCommandValidator()
    {
        RuleFor(x => x.LeadId).NotEmpty();
        RuleFor(x => x.Notes).NotEmpty();
    }
}

public class AddLeadActivityCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<AddLeadActivityCommand, Guid>
{
    public async Task<Guid> Handle(AddLeadActivityCommand request, CancellationToken cancellationToken)
    {
        var leadExists = await db.Leads.AnyAsync(l => l.Id == request.LeadId, cancellationToken);
        if (!leadExists)
        {
            throw new NotFoundException(nameof(Lead), request.LeadId);
        }

        var activity = new LeadActivity
        {
            LeadId = request.LeadId,
            Type = request.Type,
            Notes = request.Notes,
            DueDate = request.DueDate,
            CreatedByUserId = currentUser.UserId,
            CreatedAt = dateTimeProvider.UtcNow
        };

        db.LeadActivities.Add(activity);
        await db.SaveChangesAsync(cancellationToken);

        return activity.Id;
    }
}
