using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Nutrition.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Nutrition.Queries;

public record GetFoodItemsListQuery : IQuery<List<FoodItemDto>>;

public class GetFoodItemsListQueryHandler(IApplicationDbContext db) : IRequestHandler<GetFoodItemsListQuery, List<FoodItemDto>>
{
    public Task<List<FoodItemDto>> Handle(GetFoodItemsListQuery request, CancellationToken cancellationToken)
        => db.FoodItems.AsNoTracking()
            .OrderBy(f => f.Name)
            .Select(f => new FoodItemDto(f.Id, f.Name, f.CaloriesPerServing, f.ProteinG, f.CarbsG, f.FatG, f.ServingSizeDescription))
            .ToListAsync(cancellationToken);
}
