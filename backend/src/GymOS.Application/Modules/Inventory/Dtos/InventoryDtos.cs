using GymOS.Domain.Inventory;

namespace GymOS.Application.Modules.Inventory.Dtos;

public record InventoryItemListDto(
    Guid Id, string Sku, string Name, InventoryCategory Category, int QuantityOnHand, int ReorderLevel, bool IsLowStock,
    decimal? UnitPrice);

public record StockMovementDto(Guid Id, StockMovementType Type, int Quantity, string? Reason, DateTimeOffset MovedAt);

public record PurchaseRecordDto(Guid Id, string? SupplierName, int Quantity, decimal UnitCost, DateOnly PurchasedAt, string? InvoiceReference);

public record InventoryItemDetailDto(
    Guid Id, string Sku, string Name, InventoryCategory Category, int QuantityOnHand, int ReorderLevel, bool IsLowStock,
    decimal? UnitCost, decimal? UnitPrice, Guid BranchId,
    IReadOnlyList<StockMovementDto> StockMovements, IReadOnlyList<PurchaseRecordDto> PurchaseRecords);
