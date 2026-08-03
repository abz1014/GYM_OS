using GymOS.Domain.Memberships;
using Shouldly;

namespace GymOS.Domain.Tests.Memberships;

public class CouponTests
{
    [Fact]
    public void IsRedeemable_is_true_when_active_and_unlimited_redemptions()
    {
        var coupon = new Coupon { IsActive = true, MaxRedemptions = null, TimesRedeemed = 500 };

        coupon.IsRedeemable.ShouldBeTrue();
    }

    [Fact]
    public void IsRedeemable_is_true_when_under_the_redemption_cap()
    {
        var coupon = new Coupon { IsActive = true, MaxRedemptions = 10, TimesRedeemed = 9 };

        coupon.IsRedeemable.ShouldBeTrue();
    }

    [Fact]
    public void IsRedeemable_is_false_once_the_redemption_cap_is_reached()
    {
        var coupon = new Coupon { IsActive = true, MaxRedemptions = 10, TimesRedeemed = 10 };

        coupon.IsRedeemable.ShouldBeFalse();
    }

    [Fact]
    public void IsRedeemable_is_false_when_inactive_regardless_of_redemption_count()
    {
        var coupon = new Coupon { IsActive = false, MaxRedemptions = null, TimesRedeemed = 0 };

        coupon.IsRedeemable.ShouldBeFalse();
    }
}
