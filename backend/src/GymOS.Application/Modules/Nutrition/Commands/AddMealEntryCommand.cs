using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Nutrition;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Nutrition.Commands;

public record AddMealEntryCommand(Guid DietPlanId, Guid FoodItemId, MealType MealType, decimal Quantity) : ICommand<Guid>;

public class AddMealEntryCommandValidator : AbstractValidator<AddMealEntryCommand>
{
    public AddMealEntryCommandValidator()
    {
        RuleFor(x => x.DietPlanId).NotEmpty();
        RuleFor(x => x.FoodItemId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}

public class AddMealEntryCommandHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<AddMealEntryCommand, Guid>
{
    public async Task<Guid> Handle(AddMealEntryCommand request, CancellationToken cancellationToken)
    {
        var plan = await db.DietPlans.FirstOrDefaultAsync(p => p.Id == request.DietPlanId, cancellationToken)
            ?? throw new NotFoundException(nameof(DietPlan), request.DietPlanId);

        var consumedAt = dateTimeProvider.UtcNow;
        var entry = new MealEntry
        {
            DietPlanId = request.DietPlanId,
            FoodItemId = request.FoodItemId,
            MealType = request.MealType,
            Quantity = request.Quantity,
            ConsumedAt = consumedAt
        };

        db.MealEntries.Add(entry);
        plan.RaiseMealLogged(DateOnly.FromDateTime(consumedAt.UtcDateTime));
        await db.SaveChangesAsync(cancellationToken);

        return entry.Id;
    }
}
