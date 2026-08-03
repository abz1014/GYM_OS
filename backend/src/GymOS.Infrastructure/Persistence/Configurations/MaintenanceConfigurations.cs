using GymOS.Domain.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymOS.Infrastructure.Persistence.Configurations;

public class WorkOrderConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public void Configure(EntityTypeBuilder<WorkOrder> builder)
    {
        builder.Property(w => w.Title).HasMaxLength(200).IsRequired();
        builder.HasOne(w => w.Asset).WithMany().HasForeignKey(w => w.AssetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(w => w.DowntimeLogs).WithOne(d => d.WorkOrder).HasForeignKey(d => d.WorkOrderId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class MaintenanceScheduleConfiguration : IEntityTypeConfiguration<MaintenanceSchedule>
{
    public void Configure(EntityTypeBuilder<MaintenanceSchedule> builder)
    {
        builder.Property(s => s.RecurrenceRule).HasMaxLength(200).IsRequired();
        builder.HasOne(s => s.Asset).WithMany().HasForeignKey(s => s.AssetId).OnDelete(DeleteBehavior.Cascade);
    }
}
