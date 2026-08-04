using GymOS.Domain.Classes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymOS.Infrastructure.Persistence.Configurations;

public class ClassTypeConfiguration : IEntityTypeConfiguration<ClassType>
{
    public void Configure(EntityTypeBuilder<ClassType> builder)
    {
        builder.Property(c => c.Name).HasMaxLength(120).IsRequired();
        builder.Property(c => c.ColorHex).HasMaxLength(9);
    }
}

public class ClassScheduleConfiguration : IEntityTypeConfiguration<ClassSchedule>
{
    public void Configure(EntityTypeBuilder<ClassSchedule> builder)
    {
        builder.Property(s => s.Location).HasMaxLength(120);
        builder.HasOne(s => s.ClassType).WithMany().HasForeignKey(s => s.ClassTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.Trainer).WithMany().HasForeignKey(s => s.TrainerId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(s => s.BranchId);
    }
}

public class ClassSessionConfiguration : IEntityTypeConfiguration<ClassSession>
{
    public void Configure(EntityTypeBuilder<ClassSession> builder)
    {
        builder.Property(s => s.Location).HasMaxLength(120);
        builder.HasOne(s => s.ClassSchedule).WithMany().HasForeignKey(s => s.ClassScheduleId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(s => s.ClassType).WithMany().HasForeignKey(s => s.ClassTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.Trainer).WithMany().HasForeignKey(s => s.TrainerId).OnDelete(DeleteBehavior.SetNull);
        // Bookings (Step 2) and the member-facing "what's on this week" query both filter by branch
        // + time window, so index the pair that query will hit.
        builder.HasIndex(s => new { s.BranchId, s.StartsAt });
    }
}
