using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Inventory.Dtos;
using GymOS.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Inventory.Queries;

public record GetInventoryItemByIdQuery(Guid Id) : IQuery<InventoryItemDetailDto>;

public class GetInventoryItemByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetInventoryItemByIdQuery, InventoryItemDetailDto>
{
    public async Task<InventoryItemDetailDto> Handle(GetInventoryItemByIdQuery request, CancellationToken cancellationToken)
    {
        var item = await db.InventoryItems.AsNoTracking().FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(InventoryItem), request.Id);

        var movements = await db.StockMovements.AsNoTracking()
            .Where(m => m.InventoryItemId == request.Id)
            .OrderByDescending(m => m.MovedAt)
            .Take(50)
            .Select(m => new StockMovementDto(m.Id, m.Type, m.Quantity, m.Reason, m.MovedAt))
            .ToListAsync(cancellationToken);

        var purchases = await db.PurchaseRecords.AsNoTracking()
            .Include(p => p.Supplier)
            .Where(p => p.InventoryItemId == request.Id)
            .OrderByDescending(p => p.PurchasedAt)
            .Select(p => new PurchaseRecordDto(p.Id, p.Supplier!.Name, p.Quantity, p.UnitCost, p.PurchasedAt, p.InvoiceReference))
            .ToListAsync(cancellationToken);

        return new InventoryItemDetailDto(
            item.Id, item.Sku, item.Name, item.Category, item.QuantityOnHand, item.ReorderLevel,
            item.QuantityOnHand <= item.ReorderLevel, item.UnitCost, item.UnitPrice, item.BranchId, movements, purchases);
    }
}
