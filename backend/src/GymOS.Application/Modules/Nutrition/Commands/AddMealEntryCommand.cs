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
        var planExists = await db.DietPlans.AnyAsync(p => p.Id == request.DietPlanId, cancellationToken);
        if (!planExists)
        {
            throw new NotFoundException(nameof(DietPlan), request.DietPlanId);
        }

        var entry = new MealEntry
        {
            DietPlanId = request.DietPlanId,
            FoodItemId = request.FoodItemId,
            MealType = request.MealType,
            Quantity = request.Quantity,
            ConsumedAt = dateTimeProvider.UtcNow
        };

        db.MealEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);

        return entry.Id;
    }
}
