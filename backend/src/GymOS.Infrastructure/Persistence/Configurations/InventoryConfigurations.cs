using GymOS.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymOS.Infrastructure.Persistence.Configurations;

public class InventoryItemConfigurationExtra : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.HasIndex(i => new { i.TenantId, i.Sku }).IsUnique();
        builder.Property(i => i.Sku).HasMaxLength(30).IsRequired();
        builder.Property(i => i.Name).HasMaxLength(200).IsRequired();
    }
}

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.HasOne(m => m.InventoryItem).WithMany().HasForeignKey(m => m.InventoryItemId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PurchaseRecordConfiguration : IEntityTypeConfiguration<PurchaseRecord>
{
    public void Configure(EntityTypeBuilder<PurchaseRecord> builder)
    {
        builder.HasOne(p => p.InventoryItem).WithMany().HasForeignKey(p => p.InventoryItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(p => p.Supplier).WithMany().HasForeignKey(p => p.SupplierId).OnDelete(DeleteBehavior.SetNull);
    }
}
