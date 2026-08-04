using GymOS.Application.Common.Extensions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Common.Security;
using GymOS.Application.Modules.Inventory.Dtos;
using GymOS.Domain.Inventory;
using GymOS.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Inventory.Queries;

public record GetInventoryItemsListQuery(Guid? BranchId, InventoryCategory? Category, bool? LowStockOnly, int Page = 1, int PageSize = 20)
    : IQuery<PagedList<InventoryItemListDto>>;

public class GetInventoryItemsListQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetInventoryItemsListQuery, PagedList<InventoryItemListDto>>
{
    public async Task<PagedList<InventoryItemListDto>> Handle(GetInventoryItemsListQuery request, CancellationToken cancellationToken)
    {
        var accessibleBranchIds = await BranchAccessResolver.GetAccessibleBranchIdsAsync(db, currentUser, cancellationToken);
        var query = db.InventoryItems.AsNoTracking().Where(i => accessibleBranchIds.Contains(i.BranchId));

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

        var projected = query
            .OrderBy(i => i.Name)
            .Select(i => new InventoryItemListDto(
                i.Id, i.Sku, i.Name, i.Category, i.QuantityOnHand, i.ReorderLevel, i.QuantityOnHand <= i.ReorderLevel, i.UnitPrice));

        return await projected.ToPagedListAsync(request.Page, request.PageSize, cancellationToken);
    }
}
