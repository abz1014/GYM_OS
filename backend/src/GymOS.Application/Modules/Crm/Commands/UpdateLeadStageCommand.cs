using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Members.Commands;
using GymOS.Domain.Crm;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Crm.Commands;

public record UpdateLeadStageCommand(Guid LeadId, LeadStage Stage, Guid? ConvertedMemberId) : ICommand<Unit>;

public class UpdateLeadStageCommandValidator : AbstractValidator<UpdateLeadStageCommand>
{
    public UpdateLeadStageCommandValidator() => RuleFor(x => x.LeadId).NotEmpty();
}

public class UpdateLeadStageCommandHandler(IApplicationDbContext db, ISender sender) : IRequestHandler<UpdateLeadStageCommand, Unit>
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
        else if (request.Stage == LeadStage.Member && lead.ConvertedMemberId is null)
        {
            // Moving a lead to the Member stage should produce a real Member record, not just a
            // label change — otherwise the pipeline's "converted" count is disconnected from
            // reality and nothing downstream (billing, attendance) ever sees this person.
            lead.ConvertedMemberId = await sender.Send(
                new CreateMemberCommand(lead.FirstName, lead.LastName, lead.Email, lead.Phone, null, null, null, lead.BranchId),
                cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
