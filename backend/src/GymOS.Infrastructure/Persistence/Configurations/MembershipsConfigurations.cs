using GymOS.Domain.Memberships;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymOS.Infrastructure.Persistence.Configurations;

public class MembershipPlanConfiguration : IEntityTypeConfiguration<MembershipPlan>
{
    public void Configure(EntityTypeBuilder<MembershipPlan> builder)
    {
        builder.Property(p => p.Name).HasMaxLength(150).IsRequired();
        builder.Property(p => p.Currency).HasMaxLength(3).IsRequired();
        builder.HasMany(p => p.Discounts).WithOne(d => d.MembershipPlan).HasForeignKey(d => d.MembershipPlanId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class DiscountConfiguration : IEntityTypeConfiguration<Discount>
{
    public void Configure(EntityTypeBuilder<Discount> builder)
    {
        builder.Property(d => d.Name).HasMaxLength(150).IsRequired();
        builder.HasMany(d => d.Coupons).WithOne(c => c.Discount).HasForeignKey(c => c.DiscountId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> builder)
    {
        builder.HasIndex(c => new { c.TenantId, c.Code }).IsUnique();
        builder.Property(c => c.Code).HasMaxLength(50).IsRequired();
        builder.Ignore(c => c.IsRedeemable);
    }
}
