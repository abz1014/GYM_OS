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

        // Self-reference: who brought this member in. SetNull — losing the referrer must never
        // take the referred member down with it. Indexed for the top-referrers aggregation.
        builder.HasOne(m => m.ReferredByMember).WithMany().HasForeignKey(m => m.ReferredByMemberId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(m => m.ReferredByMemberId);

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

public class MemberGoalConfiguration : IEntityTypeConfiguration<MemberGoal>
{
    public void Configure(EntityTypeBuilder<MemberGoal> builder)
    {
        builder.Property(g => g.Title).HasMaxLength(200).IsRequired();
        builder.HasOne(g => g.Member).WithMany().HasForeignKey(g => g.MemberId).OnDelete(DeleteBehavior.Cascade);
        // The portal reads "this member's goals, open first" — index the member.
        builder.HasIndex(g => g.MemberId);
    }
}

public class MemberTrainingPreferenceConfiguration : IEntityTypeConfiguration<MemberTrainingPreference>
{
    public void Configure(EntityTypeBuilder<MemberTrainingPreference> builder)
    {
        builder.HasOne(p => p.Member).WithMany().HasForeignKey(p => p.MemberId).OnDelete(DeleteBehavior.Cascade);
        // Exactly one preference row per member: the upsert in SetMyWeeklyGoalCommand reads by member
        // and expects at most one, so the database enforces that rather than trusting it.
        builder.HasIndex(p => p.MemberId).IsUnique();
    }
}
