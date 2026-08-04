using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Nutrition.Dtos;
using GymOS.Domain.Nutrition;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Nutrition.Queries;

public record GetDietPlanByIdQuery(Guid Id) : IQuery<DietPlanDetailDto>;

public class GetDietPlanByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetDietPlanByIdQuery, DietPlanDetailDto>
{
    public async Task<DietPlanDetailDto> Handle(GetDietPlanByIdQuery request, CancellationToken cancellationToken)
    {
        var plan = await db.DietPlans.AsNoTracking()
            .Include(p => p.MealEntries)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(DietPlan), request.Id);

        var foodItemIds = plan.MealEntries.Select(e => e.FoodItemId).Distinct().ToList();
        var foodItemNames = await db.FoodItems.AsNoTracking()
            .Where(f => foodItemIds.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, f => f.Name, cancellationToken);

        return new DietPlanDetailDto(
            plan.Id, plan.MemberId, plan.Name, plan.TargetCalories, plan.TargetProteinG, plan.TargetCarbsG, plan.TargetFatG,
            plan.StartDate, plan.EndDate,
            plan.MealEntries
                .OrderByDescending(e => e.ConsumedAt)
                .Select(e => new MealEntryDto(e.Id, e.FoodItemId, foodItemNames.GetValueOrDefault(e.FoodItemId, string.Empty), e.MealType, e.Quantity, e.ConsumedAt))
                .ToList());
    }
}
