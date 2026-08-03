using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Memberships;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Memberships.Commands;

public record CreateDiscountCommand(
    string Name, DiscountType Type, decimal Value, Guid? MembershipPlanId, DateOnly? ValidFrom, DateOnly? ValidTo) : ICommand<Guid>;

public class CreateDiscountCommandValidator : AbstractValidator<CreateDiscountCommand>
{
    public CreateDiscountCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Value).GreaterThan(0);
        RuleFor(x => x.Value).LessThanOrEqualTo(100).When(x => x.Type == DiscountType.Percentage);
    }
}

public class CreateDiscountCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<CreateDiscountCommand, Guid>
{
    public async Task<Guid> Handle(CreateDiscountCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new ForbiddenAccessException("No tenant context.");

        var discount = new Discount
        {
            TenantId = tenantId,
            Name = request.Name,
            Type = request.Type,
            Value = request.Value,
            MembershipPlanId = request.MembershipPlanId,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            IsActive = true
        };

        db.Discounts.Add(discount);
        await db.SaveChangesAsync(cancellationToken);

        return discount.Id;
    }
}

public record CreateCouponCommand(string Code, Guid DiscountId, int? MaxRedemptions, DateOnly? ValidFrom, DateOnly? ValidTo) : ICommand<Guid>;

public class CreateCouponCommandValidator : AbstractValidator<CreateCouponCommand>
{
    public CreateCouponCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.DiscountId).NotEmpty();
    }
}

public class CreateCouponCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<CreateCouponCommand, Guid>
{
    public async Task<Guid> Handle(CreateCouponCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new ForbiddenAccessException("No tenant context.");

        var discountExists = await db.Discounts.AnyAsync(d => d.Id == request.DiscountId, cancellationToken);
        if (!discountExists)
        {
            throw new NotFoundException(nameof(Discount), request.DiscountId);
        }

        var codeTaken = await db.Coupons.AnyAsync(c => c.Code == request.Code, cancellationToken);
        if (codeTaken)
        {
            throw new ValidationException($"Coupon code \"{request.Code}\" is already in use.");
        }

        var coupon = new Coupon
        {
            TenantId = tenantId,
            Code = request.Code,
            DiscountId = request.DiscountId,
            MaxRedemptions = request.MaxRedemptions,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            IsActive = true
        };

        db.Coupons.Add(coupon);
        await db.SaveChangesAsync(cancellationToken);

        return coupon.Id;
    }
}
