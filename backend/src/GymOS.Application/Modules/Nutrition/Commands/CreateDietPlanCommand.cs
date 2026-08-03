using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Nutrition;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Nutrition.Commands;

public record CreateDietPlanCommand(Guid MemberId, string Name, decimal? TargetCalories, DateOnly StartDate, DateOnly? EndDate) : ICommand<Guid>;

public class CreateDietPlanCommandValidator : AbstractValidator<CreateDietPlanCommand>
{
    public CreateDietPlanCommandValidator()
    {
        RuleFor(x => x.MemberId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
    }
}

public class CreateDietPlanCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<CreateDietPlanCommand, Guid>
{
    public async Task<Guid> Handle(CreateDietPlanCommand request, CancellationToken cancellationToken)
    {
        var memberExists = await db.Members.AnyAsync(m => m.Id == request.MemberId, cancellationToken);
        if (!memberExists)
        {
            throw new NotFoundException(nameof(Domain.Members.Member), request.MemberId);
        }

        var plan = new DietPlan
        {
            MemberId = request.MemberId,
            Name = request.Name,
            CreatedByUserId = currentUser.UserId,
            TargetCalories = request.TargetCalories,
            StartDate = request.StartDate,
            EndDate = request.EndDate
        };

        db.DietPlans.Add(plan);
        await db.SaveChangesAsync(cancellationToken);

        return plan.Id;
    }
}
