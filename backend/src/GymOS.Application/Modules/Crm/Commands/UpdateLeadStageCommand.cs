using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Crm;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Crm.Commands;

public record UpdateLeadStageCommand(Guid LeadId, LeadStage Stage, Guid? ConvertedMemberId) : ICommand<Unit>;

public class UpdateLeadStageCommandValidator : AbstractValidator<UpdateLeadStageCommand>
{
    public UpdateLeadStageCommandValidator() => RuleFor(x => x.LeadId).NotEmpty();
}

public class UpdateLeadStageCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateLeadStageCommand, Unit>
{
    public async Task<Unit> Handle(UpdateLeadStageCommand request, CancellationToken cancellationToken)
    {
        var lead = await db.Leads.FirstOrDefaultAsync(l => l.Id == request.LeadId, cancellationToken)
            ?? throw new NotFoundException(nameof(Lead), request.LeadId);

        lead.Stage = request.Stage;
        if (request.ConvertedMemberId is not null)
        {
            lead.ConvertedMemberId = request.ConvertedMemberId;
        }

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
