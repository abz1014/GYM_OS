using GymOS.Domain.Members;
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
        builder.HasMany(a => a.Sessions).WithOne(s => s.TrainerAssignment).HasForeignKey(s => s.TrainerAssignmentId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class TrainerRatingConfiguration : IEntityTypeConfiguration<TrainerRating>
{
    public void Configure(EntityTypeBuilder<TrainerRating> builder)
    {
        builder.HasOne(r => r.Member).WithMany().HasForeignKey(r => r.MemberId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Session).WithMany().HasForeignKey(r => r.SessionId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class CoachMessageConfiguration : IEntityTypeConfiguration<CoachMessage>
{
    public void Configure(EntityTypeBuilder<CoachMessage> builder)
    {
        builder.Property(m => m.Body).HasMaxLength(CoachMessagePolicy.MaxBodyLength).IsRequired();
        builder.Property(m => m.Author).HasConversion<string>().HasMaxLength(20);

        // Reading a conversation is the common query, and it is always "this pairing, oldest first".
        builder.HasIndex(m => new { m.TrainerId, m.MemberId, m.SentAt });

        // A pairing may end and its history must survive it — the correspondence is the member's
        // record of what they were told, not an attachment to a currently-valid assignment.
        builder.HasOne<Trainer>().WithMany().HasForeignKey(m => m.TrainerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Member>().WithMany().HasForeignKey(m => m.MemberId).OnDelete(DeleteBehavior.Restrict);
    }
}
