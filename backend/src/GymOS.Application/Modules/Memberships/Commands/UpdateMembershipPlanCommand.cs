using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Memberships;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Memberships.Commands;

public record UpdateMembershipPlanCommand(
    Guid Id, string Name, string? Description, decimal Price, int MaxFreezeDays, bool IsActive) : ICommand<Unit>;

public class UpdateMembershipPlanCommandValidator : AbstractValidator<UpdateMembershipPlanCommand>
{
    public UpdateMembershipPlanCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxFreezeDays).GreaterThanOrEqualTo(0);
    }
}

public class UpdateMembershipPlanCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateMembershipPlanCommand, Unit>
{
    public async Task<Unit> Handle(UpdateMembershipPlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await db.MembershipPlans.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(MembershipPlan), request.Id);

        plan.Name = request.Name;
        plan.Description = request.Description;
        plan.Price = request.Price;
        plan.MaxFreezeDays = request.MaxFreezeDays;
        plan.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
