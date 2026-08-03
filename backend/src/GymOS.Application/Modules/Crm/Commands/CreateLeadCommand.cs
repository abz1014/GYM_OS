using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Crm;
using MediatR;

namespace GymOS.Application.Modules.Crm.Commands;

public record CreateLeadCommand(
    string FirstName, string LastName, string Email, string? Phone, LeadSource Source, Guid BranchId, Guid? AssignedToUserId) : ICommand<Guid>;

public class CreateLeadCommandValidator : AbstractValidator<CreateLeadCommand>
{
    public CreateLeadCommandValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.BranchId).NotEmpty();
    }
}

public class CreateLeadCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser) : IRequestHandler<CreateLeadCommand, Guid>
{
    public async Task<Guid> Handle(CreateLeadCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new ForbiddenAccessException("No tenant context.");

        var lead = new Lead
        {
            TenantId = tenantId,
            BranchId = request.BranchId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            Phone = request.Phone,
            Source = request.Source,
            Stage = LeadStage.Lead,
            AssignedToUserId = request.AssignedToUserId
        };

        db.Leads.Add(lead);
        await db.SaveChangesAsync(cancellationToken);

        return lead.Id;
    }
}
