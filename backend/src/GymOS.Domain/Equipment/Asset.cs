using GymOS.Domain.Common;

namespace GymOS.Domain.Equipment;

public class Asset : BaseEntity, IBranchScoped, IAuditable
{
    public Guid TenantId { get; set; }

    public Guid BranchId { get; set; }

    public string AssetTag { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Category { get; set; }

    public string QrCodeToken { get; set; } = string.Empty;

    public List<string> PhotoUrls { get; set; } = [];

    public string? ManualUrl { get; set; }

    public DateOnly? WarrantyExpiresAt { get; set; }

    public Guid? SupplierId { get; set; }

    public Supplier? Supplier { get; set; }

    public AssetStatus Status { get; set; } = AssetStatus.Active;

    public DateOnly? PurchaseDate { get; set; }

    public decimal? PurchasePrice { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedByUserId { get; set; }
}
