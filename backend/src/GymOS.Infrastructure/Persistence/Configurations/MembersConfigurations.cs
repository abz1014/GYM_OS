using GymOS.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymOS.Infrastructure.Persistence.Configurations;

public class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> builder)
    {
        builder.HasIndex(m => new { m.TenantId, m.MemberCode }).IsUnique();
        builder.HasIndex(m => m.Email);
        builder.Property(m => m.MemberCode).HasMaxLength(20).IsRequired();
        builder.Property(m => m.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(m => m.LastName).HasMaxLength(100).IsRequired();
        builder.Property(m => m.Email).HasMaxLength(256).IsRequired();
        builder.Property(m => m.QrCodeToken).IsRequired();

        builder.HasOne(m => m.User).WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(m => m.EmergencyContacts).WithOne(c => c.Member).HasForeignKey(c => c.MemberId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(m => m.MedicalNotes).WithOne(n => n.Member).HasForeignKey(n => n.MemberId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(m => m.Measurements).WithOne(x => x.Member).HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(m => m.ProgressPhotos).WithOne(p => p.Member).HasForeignKey(p => p.MemberId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(m => m.MemberMemberships).WithOne(mm => mm.Member).HasForeignKey(mm => mm.MemberId).OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(m => m.FullName);
    }
}

public class MemberMembershipConfiguration : IEntityTypeConfiguration<MemberMembership>
{
    public void Configure(EntityTypeBuilder<MemberMembership> builder)
    {
        builder.HasOne(mm => mm.MembershipPlan).WithMany().HasForeignKey(mm => mm.MembershipPlanId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(mm => mm.Currency).HasMaxLength(3).IsRequired();
        builder.HasIndex(mm => mm.EndDate);
    }
}
