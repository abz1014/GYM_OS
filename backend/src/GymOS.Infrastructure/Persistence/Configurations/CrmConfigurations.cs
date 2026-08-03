using GymOS.Domain.Crm;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymOS.Infrastructure.Persistence.Configurations;

public class LeadConfiguration : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> builder)
    {
        builder.Ignore(l => l.FullName);
        builder.Property(l => l.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(l => l.LastName).HasMaxLength(100).IsRequired();
        builder.Property(l => l.Email).HasMaxLength(256).IsRequired();
        builder.HasIndex(l => new { l.TenantId, l.Stage });
        builder.HasMany(l => l.Activities).WithOne(a => a.Lead).HasForeignKey(a => a.LeadId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class LeadActivityConfiguration : IEntityTypeConfiguration<LeadActivity>
{
    public void Configure(EntityTypeBuilder<LeadActivity> builder)
    {
        builder.Property(a => a.Notes).HasMaxLength(1000).IsRequired();
    }
}
