using GymOS.Domain.Inventory;
using GymOS.Domain.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymOS.Infrastructure.Persistence.Configurations;

// Get-only computed properties on Wave 2/3 entities — EF Core's conventions skip properties with
// no setter anyway, but these are explicit for clarity/safety rather than relying on that.

public class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder) => builder.Ignore(i => i.IsLowStock);
}

public class DowntimeLogConfiguration : IEntityTypeConfiguration<DowntimeLog>
{
    public void Configure(EntityTypeBuilder<DowntimeLog> builder) => builder.Ignore(d => d.Duration);
}
