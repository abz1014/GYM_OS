using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Memberships;
using MediatR;

namespace GymOS.Application.Modules.Memberships.Commands;

public record CreateMembershipPlanCommand(
    string Name, MembershipPlanType Type, string? Description,
    int DurationDays, decimal Price, string Currency, int MaxFreezeDays) : ICommand<Guid>;

public class CreateMembershipPlanCommandValidator : AbstractValidator<CreateMembershipPlanCommand>
{
    public CreateMembershipPlanCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.DurationDays).GreaterThan(0);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.MaxFreezeDays).GreaterThanOrEqualTo(0);
    }
}

public class CreateMembershipPlanCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<CreateMembershipPlanCommand, Guid>
{
    public async Task<Guid> Handle(CreateMembershipPlanCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new ForbiddenAccessException("No tenant context.");

        var plan = new MembershipPlan
        {
            TenantId = tenantId,
            Name = request.Name,
            Type = request.Type,
            Description = request.Description,
            DurationDays = request.DurationDays,
            Price = request.Price,
            Currency = request.Currency,
            MaxFreezeDays = request.MaxFreezeDays,
            IsActive = true
        };

        db.MembershipPlans.Add(plan);
        await db.SaveChangesAsync(cancellationToken);

        return plan.Id;
    }
}
