using GymOS.Domain.Common;

namespace GymOS.Domain.Memberships;

public class Coupon : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public string Code { get; set; } = string.Empty;

    public Guid DiscountId { get; set; }

    public Discount? Discount { get; set; }

    public int? MaxRedemptions { get; set; }

    public int TimesRedeemed { get; set; } = 0;

    public DateOnly? ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsRedeemable => IsActive && (MaxRedemptions is null || TimesRedeemed < MaxRedemptions);
}
