using GymOS.Application.Modules.Members.Commands;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Identity;
using GymOS.Domain.Members;
using GymOS.Domain.Memberships;
using GymOS.Domain.Tenancy;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Members;

/// <summary>
/// RenewMembershipCommand internally calls sender.Send(CreateInvoiceCommand) — the same nested-
/// command pattern manually verified live in the running app (renew -> both RenewMembershipCommand
/// and CreateInvoiceCommand get their own audit rows, sharing one DB transaction). These tests lock
/// that behavior down so a future refactor can't silently break it.
/// </summary>
public class RenewMembershipCommandHandlerTests : ApplicationTestBase
{
    [Fact]
    public async Task Renewal_creates_a_linked_invoice_and_audits_both_commands()
    {
        var ctx = await SeedAsync();
        SetAuthenticatedAs(ctx.TenantId, ctx.StaffUserId);

        var membershipId = await SendAsync(new RenewMembershipCommand(
            ctx.MemberId, ctx.PlanId, new DateOnly(2026, 1, 15), AutoRenew: false, CouponCode: null));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var membership = await db.MemberMemberships.SingleAsync(m => m.Id == membershipId);
        membership.InvoiceId.ShouldNotBeNull();

        var invoice = await db.Invoices.Include(i => i.Lines).SingleAsync(i => i.Id == membership.InvoiceId);
        invoice.MemberId.ShouldBe(ctx.MemberId);
        invoice.TotalAmount.ShouldBe(150m);
        invoice.Lines.ShouldHaveSingleItem();

        var auditActions = await db.AuditLogs.IgnoreQueryFilters()
            .Where(a => a.TenantId == ctx.TenantId)
            .Select(a => a.Action)
            .ToListAsync();

        auditActions.ShouldContain(nameof(RenewMembershipCommand));
        auditActions.ShouldContain("CreateInvoiceCommand");
    }

    [Fact]
    public async Task A_future_start_date_leaves_the_membership_pending_activation()
    {
        var ctx = await SeedAsync();
        SetAuthenticatedAs(ctx.TenantId, ctx.StaffUserId);

        // RenewMembershipCommandHandler compares against the real system clock (DateTime.UtcNow),
        // not the injected IDateTimeProvider, so the "future" date has to be relative to it too.
        var membershipId = await SendAsync(new RenewMembershipCommand(
            ctx.MemberId, ctx.PlanId, DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)), AutoRenew: false, CouponCode: null));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var membership = await db.MemberMemberships.SingleAsync(m => m.Id == membershipId);

        membership.Status.ShouldBe(MemberMembershipStatus.PendingActivation);
    }

    [Fact]
    public async Task A_valid_percentage_coupon_discounts_the_invoice_and_increments_redemptions()
    {
        var ctx = await SeedAsync();
        var (couponId, couponCode) = await SeedPercentageCouponAsync(ctx.TenantId, ctx.PlanId, percentOff: 20);
        SetAuthenticatedAs(ctx.TenantId, ctx.StaffUserId);

        await SendAsync(new RenewMembershipCommand(
            ctx.MemberId, ctx.PlanId, new DateOnly(2026, 1, 15), AutoRenew: false, CouponCode: couponCode));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var membership = await db.MemberMemberships.SingleAsync(m => m.MemberId == ctx.MemberId);
        membership.PricePaid.ShouldBe(120m); // 150 - 20%

        var invoice = await db.Invoices.SingleAsync(i => i.Id == membership.InvoiceId);
        invoice.DiscountAmount.ShouldBe(30m);

        var coupon = await db.Coupons.SingleAsync(c => c.Id == couponId);
        coupon.TimesRedeemed.ShouldBe(1);
    }

    [Fact]
    public async Task An_invalid_coupon_fails_atomically_with_no_partial_membership_or_invoice()
    {
        var ctx = await SeedAsync();
        SetAuthenticatedAs(ctx.TenantId, ctx.StaffUserId);

        var act = () => SendAsync(new RenewMembershipCommand(
            ctx.MemberId, ctx.PlanId, new DateOnly(2026, 1, 15), AutoRenew: false, CouponCode: "DOES-NOT-EXIST"));

        await Should.ThrowAsync<FluentValidation.ValidationException>(act);

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        (await db.MemberMemberships.AnyAsync(m => m.MemberId == ctx.MemberId)).ShouldBeFalse();
        (await db.Invoices.AnyAsync(i => i.MemberId == ctx.MemberId)).ShouldBeFalse();
        (await db.AuditLogs.IgnoreQueryFilters().AnyAsync(a => a.TenantId == ctx.TenantId)).ShouldBeFalse();
    }

    private void SetAuthenticatedAs(Guid tenantId, Guid userId)
    {
        CurrentUser.TenantId = tenantId;
        CurrentUser.UserId = userId;
        CurrentUser.IsAuthenticated = true;
    }

    private async Task<(Guid TenantId, Guid BranchId, Guid MemberId, Guid PlanId, Guid StaffUserId)> SeedAsync()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);

        var branch = new Branch { TenantId = tenant.Id, Name = "Main", AddressLine = "1 Main St", City = "City", Country = "US" };
        db.Branches.Add(branch);

        // AuditLog.UserId has an FK to Users — the "staff member performing the action" needs to
        // be a real row, not just an opaque Guid, for the audit-write SaveChanges to succeed.
        var staffUser = new User
        {
            TenantId = tenant.Id,
            Email = $"{Guid.NewGuid():N}@example.com",
            PasswordHash = "unused-in-this-test",
            FirstName = "Staff",
            LastName = "User"
        };
        db.Users.Add(staffUser);
        db.UserBranchAccesses.Add(new UserBranchAccess { UserId = staffUser.Id, BranchId = branch.Id });

        var member = new Member
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12],
            FirstName = "Test",
            LastName = "Member",
            Email = $"{Guid.NewGuid():N}@example.com",
            JoinDate = new DateOnly(2025, 1, 1),
            QrCodeToken = Guid.NewGuid().ToString("N")
        };
        db.Members.Add(member);

        var plan = new MembershipPlan
        {
            TenantId = tenant.Id,
            Name = "Standard Monthly",
            Type = MembershipPlanType.Monthly,
            DurationDays = 30,
            Price = 150m,
            Currency = "USD",
            MaxFreezeDays = 14
        };
        db.MembershipPlans.Add(plan);

        await db.SaveChangesAsync();
        return (tenant.Id, branch.Id, member.Id, plan.Id, staffUser.Id);
    }

    private async Task<(Guid CouponId, string Code)> SeedPercentageCouponAsync(Guid tenantId, Guid planId, decimal percentOff)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var discount = new Discount
        {
            TenantId = tenantId,
            MembershipPlanId = planId,
            Name = $"{percentOff}% off",
            Type = DiscountType.Percentage,
            Value = percentOff
        };
        db.Discounts.Add(discount);

        var code = $"SAVE{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var coupon = new Coupon { TenantId = tenantId, Code = code, DiscountId = discount.Id };
        db.Coupons.Add(coupon);

        await db.SaveChangesAsync();
        return (coupon.Id, code);
    }
}
