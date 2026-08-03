using GymOS.Domain.Common;

namespace GymOS.Domain.Memberships;

public class Discount : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid? MembershipPlanId { get; set; }

    public MembershipPlan? MembershipPlan { get; set; }

    public string Name { get; set; } = string.Empty;

    public DiscountType Type { get; set; }

    public decimal Value { get; set; }

    public DateOnly? ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Coupon> Coupons { get; set; } = [];
}
