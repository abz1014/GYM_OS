using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Nutrition;
using MediatR;

namespace GymOS.Application.Modules.Nutrition.Commands;

public record CreateFoodItemCommand(string Name, decimal CaloriesPerServing, decimal ProteinG, decimal CarbsG, decimal FatG, string ServingSizeDescription)
    : ICommand<Guid>;

public class CreateFoodItemCommandValidator : AbstractValidator<CreateFoodItemCommand>
{
    public CreateFoodItemCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.CaloriesPerServing).GreaterThanOrEqualTo(0);
    }
}

public class CreateFoodItemCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<CreateFoodItemCommand, Guid>
{
    public async Task<Guid> Handle(CreateFoodItemCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new ForbiddenAccessException("No tenant context.");

        var foodItem = new FoodItem
        {
            TenantId = tenantId,
            Name = request.Name,
            CaloriesPerServing = request.CaloriesPerServing,
            ProteinG = request.ProteinG,
            CarbsG = request.CarbsG,
            FatG = request.FatG,
            ServingSizeDescription = request.ServingSizeDescription
        };

        db.FoodItems.Add(foodItem);
        await db.SaveChangesAsync(cancellationToken);

        return foodItem.Id;
    }
}
