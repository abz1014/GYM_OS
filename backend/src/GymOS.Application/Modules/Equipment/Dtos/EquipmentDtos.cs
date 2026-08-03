using GymOS.Domain.Equipment;

namespace GymOS.Application.Modules.Equipment.Dtos;

public record SupplierDto(Guid Id, string Name, string? ContactName, string? Phone, string? Email);

public record AssetListItemDto(
    Guid Id, string AssetTag, string Name, string? Category, AssetStatus Status, DateOnly? WarrantyExpiresAt, string? SupplierName);

public record AssetDetailDto(
    Guid Id, string AssetTag, string Name, string? Category, string QrCodeToken, List<string> PhotoUrls, string? ManualUrl,
    DateOnly? WarrantyExpiresAt, Guid? SupplierId, string? SupplierName, AssetStatus Status, DateOnly? PurchaseDate,
    decimal? PurchasePrice, string? Notes, Guid BranchId);
