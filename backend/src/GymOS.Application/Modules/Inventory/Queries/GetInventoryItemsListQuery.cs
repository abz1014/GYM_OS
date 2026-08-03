using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Inventory.Dtos;
using GymOS.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Inventory.Queries;

public record GetInventoryItemsListQuery(Guid? BranchId, InventoryCategory? Category, bool? LowStockOnly) : IQuery<List<InventoryItemListDto>>;

public class GetInventoryItemsListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetInventoryItemsListQuery, List<InventoryItemListDto>>
{
    public Task<List<InventoryItemListDto>> Handle(GetInventoryItemsListQuery request, CancellationToken cancellationToken)
    {
        var query = db.InventoryItems.AsNoTracking().AsQueryable();

        if (request.BranchId is not null)
        {
            query = query.Where(i => i.BranchId == request.BranchId);
        }

        if (request.Category is not null)
        {
            query = query.Where(i => i.Category == request.Category);
        }

        if (request.LowStockOnly == true)
        {
            query = query.Where(i => i.QuantityOnHand <= i.ReorderLevel);
        }

        return query
            .OrderBy(i => i.Name)
            .Select(i => new InventoryItemListDto(
                i.Id, i.Sku, i.Name, i.Category, i.QuantityOnHand, i.ReorderLevel, i.QuantityOnHand <= i.ReorderLevel, i.UnitPrice))
            .ToListAsync(cancellationToken);
    }
}
