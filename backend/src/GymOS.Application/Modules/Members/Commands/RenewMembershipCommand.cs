using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Billing.Commands;
using GymOS.Domain.Billing;
using GymOS.Domain.Members;
using GymOS.Domain.Memberships;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Members.Commands;

/// <summary>Also used for a member's first membership signup, not only renewals.</summary>
public record RenewMembershipCommand(
    Guid MemberId,
    Guid MembershipPlanId,
    DateOnly StartDate,
    bool AutoRenew,
    string? CouponCode) : ICommand<Guid>;

public class RenewMembershipCommandValidator : AbstractValidator<RenewMembershipCommand>
{
    public RenewMembershipCommandValidator()
    {
        RuleFor(x => x.MemberId).NotEmpty();
        RuleFor(x => x.MembershipPlanId).NotEmpty();
    }
}

public class RenewMembershipCommandHandler(IApplicationDbContext db, ISender sender) : IRequestHandler<RenewMembershipCommand, Guid>
{
    public async Task<Guid> Handle(RenewMembershipCommand request, CancellationToken cancellationToken)
    {
        var member = await db.Members.FirstOrDefaultAsync(m => m.Id == request.MemberId, cancellationToken)
            ?? throw new NotFoundException(nameof(Member), request.MemberId);

        var plan = await db.MembershipPlans.FirstOrDefaultAsync(p => p.Id == request.MembershipPlanId, cancellationToken)
            ?? throw new NotFoundException(nameof(MembershipPlan), request.MembershipPlanId);

        var price = plan.Price;

        if (!string.IsNullOrWhiteSpace(request.CouponCode))
        {
            var coupon = await db.Coupons
                .Include(c => c.Discount)
                .FirstOrDefaultAsync(c => c.Code == request.CouponCode, cancellationToken);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            if (coupon?.Discount is null || !coupon.IsRedeemable
                || (coupon.ValidFrom is not null && coupon.ValidFrom > today)
                || (coupon.ValidTo is not null && coupon.ValidTo < today))
            {
                throw new ValidationException($"Coupon \"{request.CouponCode}\" is not valid.");
            }

            price = coupon.Discount.Type == DiscountType.Percentage
                ? price - price * coupon.Discount.Value / 100m
                : Math.Max(0, price - coupon.Discount.Value);

            coupon.TimesRedeemed++;
        }

        var endDate = request.StartDate.AddDays(plan.DurationDays);
        var isActiveNow = request.StartDate <= DateOnly.FromDateTime(DateTime.UtcNow);

        var memberMembership = new MemberMembership
        {
            MemberId = member.Id,
            MembershipPlanId = plan.Id,
            StartDate = request.StartDate,
            EndDate = endDate,
            Status = isActiveNow ? MemberMembershipStatus.Active : MemberMembershipStatus.PendingActivation,
            AutoRenew = request.AutoRenew,
            PricePaid = price,
            Currency = plan.Currency
        };

        db.MemberMemberships.Add(memberMembership);

        if (member.Status != MemberStatus.Frozen && isActiveNow)
        {
            member.Status = MemberStatus.Active;
        }

        // Makes Payment a real, trackable step of signup/renewal instead of just a recorded price —
        // the discount (if a coupon was applied) shows on the invoice rather than being baked silently
        // into the line price, and staff record the actual payment through the existing Billing flow.
        var discountAmount = plan.Price - price;
        memberMembership.InvoiceId = await sender.Send(
            new CreateInvoiceCommand(
                member.Id,
                member.BranchId,
                request.StartDate,
                request.StartDate.AddDays(7),
                0m,
                discountAmount,
                plan.Currency,
                request.CouponCode is not null ? $"Coupon applied: {request.CouponCode}" : null,
                [new CreateInvoiceLineInput(InvoiceLineItemType.MembershipFee, $"{plan.Name} membership", 1, plan.Price)]),
            cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return memberMembership.Id;
    }
}
