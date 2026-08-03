using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Reports.Dtos;
using GymOS.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Reports.Queries;

public record GetInventoryStockMovementReportQuery(int DaysBack = 30) : IQuery<List<InventoryStockMovementReportRowDto>>;

public class GetInventoryStockMovementReportQueryHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetInventoryStockMovementReportQuery, List<InventoryStockMovementReportRowDto>>
{
    public async Task<List<InventoryStockMovementReportRowDto>> Handle(GetInventoryStockMovementReportQuery request, CancellationToken cancellationToken)
        => await BuildAsync(db, dateTimeProvider, request.DaysBack, cancellationToken);

    internal static async Task<List<InventoryStockMovementReportRowDto>> BuildAsync(
        IApplicationDbContext db, IDateTimeProvider dateTimeProvider, int daysBack, CancellationToken cancellationToken)
    {
        var cutoff = dateTimeProvider.UtcNow.AddDays(-daysBack);

        // StockMovement isn't tenant-scoped itself — joining through InventoryItems (which is)
        // restricts results to the current tenant, same pattern as the other new reports.
        var movements = await db.StockMovements.AsNoTracking()
            .Where(m => m.MovedAt >= cutoff)
            .Join(db.InventoryItems, m => m.InventoryItemId, i => i.Id,
                (m, i) => new { m.Type, m.Quantity, i.Id, i.Name, i.Sku, i.QuantityOnHand })
            .ToListAsync(cancellationToken);

        return movements
            .GroupBy(m => new { m.Id, m.Name, m.Sku, m.QuantityOnHand })
            .Select(g =>
            {
                var totalIn = g.Where(x => x.Type == StockMovementType.In).Sum(x => x.Quantity);
                var totalOut = g.Where(x => x.Type == StockMovementType.Out).Sum(x => x.Quantity);
                return new InventoryStockMovementReportRowDto(g.Key.Name, g.Key.Sku, totalIn, totalOut, totalIn - totalOut, g.Key.QuantityOnHand);
            })
            .OrderByDescending(r => r.TotalIn + r.TotalOut)
            .ToList();
    }
}
