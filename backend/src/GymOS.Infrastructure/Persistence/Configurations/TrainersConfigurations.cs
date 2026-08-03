using GymOS.Domain.Trainers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymOS.Infrastructure.Persistence.Configurations;

public class TrainerConfiguration : IEntityTypeConfiguration<Trainer>
{
    public void Configure(EntityTypeBuilder<Trainer> builder)
    {
        builder.Property(t => t.Specialties).HasMaxLength(300);
        builder.HasOne(t => t.User).WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(t => t.Assignments).WithOne(a => a.Trainer).HasForeignKey(a => a.TrainerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(t => t.Schedules).WithOne(s => s.Trainer).HasForeignKey(s => s.TrainerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(t => t.Ratings).WithOne(r => r.Trainer).HasForeignKey(r => r.TrainerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(t => t.CommissionRecords).WithOne(c => c.Trainer).HasForeignKey(c => c.TrainerId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class TrainerAssignmentConfiguration : IEntityTypeConfiguration<TrainerAssignment>
{
    public void Configure(EntityTypeBuilder<TrainerAssignment> builder)
    {
        builder.HasOne(a => a.Member).WithMany().HasForeignKey(a => a.MemberId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class TrainerRatingConfiguration : IEntityTypeConfiguration<TrainerRating>
{
    public void Configure(EntityTypeBuilder<TrainerRating> builder)
    {
        builder.HasOne(r => r.Member).WithMany().HasForeignKey(r => r.MemberId).OnDelete(DeleteBehavior.Restrict);
    }
}
