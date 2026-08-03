using GymOS.Domain.Memberships;

namespace GymOS.Application.Modules.Memberships.Dtos;

public record MembershipPlanDto(
    Guid Id, string Name, MembershipPlanType Type, string? Description,
    int DurationDays, decimal Price, string Currency, int MaxFreezeDays, bool IsActive);

public record DiscountDto(Guid Id, string Name, DiscountType Type, decimal Value, Guid? MembershipPlanId, DateOnly? ValidFrom, DateOnly? ValidTo, bool IsActive);

public record CouponDto(Guid Id, string Code, Guid DiscountId, int? MaxRedemptions, int TimesRedeemed, DateOnly? ValidFrom, DateOnly? ValidTo, bool IsActive);
