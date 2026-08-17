using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Memberships;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Memberships.Commands;

/// <summary>
/// The kill switch for a coupon code, and its reverse.
///
/// Create was the only verb a coupon had. A code that leaked onto a deals forum, or one created
/// with the wrong discount attached, could not be stopped from the product at all — it kept
/// discounting every renewal it was typed into until somebody edited the row by hand in the
/// database. <see cref="Coupon.IsActive"/> already existed and
/// <see cref="Coupon.IsRedeemable"/> already gated redemption on it; the only thing missing was a
/// way for an owner to set it. No migration, no new permission — the flag and the gate were both
/// already there, unreachable.
///
/// Deactivating deliberately does NOT touch <see cref="Coupon.TimesRedeemed"/> or the memberships
/// already sold at the discounted price. Those redemptions really happened, and rewriting them
/// would turn a mistake about the future into a mistake about the books.
/// </summary>
public record SetCouponActiveCommand(Guid CouponId, bool IsActive) : ICommand<Unit>;

public class SetCouponActiveCommandHandler(IApplicationDbContext db) : IRequestHandler<SetCouponActiveCommand, Unit>
{
    public async Task<Unit> Handle(SetCouponActiveCommand request, CancellationToken cancellationToken)
    {
        var coupon = await db.Coupons.FirstOrDefaultAsync(c => c.Id == request.CouponId, cancellationToken)
            ?? throw new NotFoundException(nameof(Coupon), request.CouponId);

        coupon.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

/// <summary>
/// The same switch one level up: withdraw a discount from the catalogue, or put it back.
///
/// Same defect as the coupon above — a mispriced discount (50% where 5% was meant) was permanent
/// until someone reached for psql. <see cref="Discount.IsActive"/> already existed and
/// GetDiscountsQuery already hid inactive rows by default; nothing could write it.
///
/// Note what this does and does not reach: redemption is gated on the COUPON's flag
/// (<see cref="Coupon.IsRedeemable"/>), so deactivating a discount withdraws it from the plan
/// catalogue but leaves any live code that points at it still redeemable. Killing a leaked code is
/// <see cref="SetCouponActiveCommand"/>'s job, and cascading from here would be a second, hidden
/// meaning for one switch — reactivating the discount could not then tell which coupons an owner
/// had turned off deliberately.
/// </summary>
public record SetDiscountActiveCommand(Guid DiscountId, bool IsActive) : ICommand<Unit>;

public class SetDiscountActiveCommandHandler(IApplicationDbContext db) : IRequestHandler<SetDiscountActiveCommand, Unit>
{
    public async Task<Unit> Handle(SetDiscountActiveCommand request, CancellationToken cancellationToken)
    {
        var discount = await db.Discounts.FirstOrDefaultAsync(d => d.Id == request.DiscountId, cancellationToken)
            ?? throw new NotFoundException(nameof(Discount), request.DiscountId);

        discount.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
