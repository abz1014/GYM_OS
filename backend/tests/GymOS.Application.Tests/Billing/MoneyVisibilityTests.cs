using FluentValidation;
using GymOS.Application.Modules.Billing.Commands;
using GymOS.Application.Modules.Billing.Queries;
using GymOS.Application.Modules.Dashboard.Queries;
using GymOS.Application.Modules.Members.Commands;
using GymOS.Application.Modules.Memberships.Commands;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Billing;
using GymOS.Domain.Identity;
using GymOS.Domain.Members;
using GymOS.Domain.Memberships;
using GymOS.Domain.Tenancy;
using GymOS.Infrastructure.Persistence;
using GymOS.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Billing;

/// <summary>
/// The four things an owner could not see or do about their own money.
///
/// Each of these is a gap rather than a bug: the data was already correct, already written, and
/// already sitting in the database — nothing in the product could read it or act on it. That makes
/// them the expensive kind, because there is no error to notice. Revenue is simply short, and no
/// screen says why.
///
///  1. The dunning list. RecurringBillingJob writes the whole chase — who declined, the gateway's
///     reason, retries burned, who lost access — and outside the job itself the only reference to
///     RecurringBillingAttempt in the Application layer was the DbSet declaration.
///  2. Voiding. An invoice raised in error had no exit but being paid, so a duplicate sat in the
///     overdue queue forever inflating "outstanding", and the invoices list offered a "Cancelled"
///     filter tab nothing could ever populate.
///  3. The coupon kill switch. Create was the only verb a coupon had, so a leaked or mispriced code
///     discounted every renewal it was typed into until someone edited the row by hand.
///  4. Month-to-date revenue. The dashboard reported only today's takings, so an owner opening the
///     app was judging the afternoon rather than the month.
/// </summary>
public class MoneyVisibilityTests : ApplicationTestBase
{
    // ---------------------------------------------------------------------------------------
    // 1. The dunning list
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task The_dunning_list_shows_only_renewals_still_needing_a_chase()
    {
        /*
         * Succeeded attempts are excluded, and excluded on purpose. Every renewal a gym has ever
         * collected leaves one behind, so including them would bury the handful of rows that need a
         * phone call under thousands that do not — the list would technically contain the answer and
         * be useless for the job it exists to do.
         */
        var ctx = await SeedGymAsync();

        var declined = await SeedDunningAttemptAsync(ctx, "Declined", RecurringBillingStatus.Pending,
            failedAttempts: 2, failureReason: "Card declined by issuer.");
        var collected = await SeedDunningAttemptAsync(ctx, "Collected", RecurringBillingStatus.Succeeded,
            failedAttempts: 1, failureReason: null);
        var givenUp = await SeedDunningAttemptAsync(ctx, "GivenUp", RecurringBillingStatus.Abandoned,
            failedAttempts: BillingRetryPolicy.MaxAttempts, failureReason: "Card expired.",
            membershipStatus: MemberMembershipStatus.Frozen);

        var rows = await SendAsync(new GetDunningAttemptsQuery());

        rows.Select(r => r.Id).ShouldBe(new[] { declined, givenUp }, ignoreOrder: true);
        rows.ShouldNotContain(r => r.Id == collected);

        // The gateway's own words reach the screen — the whole point of reading this table is
        // knowing WHY a card failed before ringing the member.
        var chase = rows.Single(r => r.Id == declined);
        chase.LastFailureReason.ShouldBe("Card declined by issuer.");
        chase.FailedAttempts.ShouldBe(2);
        chase.MaxAttempts.ShouldBe(BillingRetryPolicy.MaxAttempts); // "2 of 4" is a real fraction
        chase.MemberName.ShouldBe("Declined Member");
        chase.InvoiceNumber.ShouldNotBeNullOrWhiteSpace();
        chase.Status.ShouldBe(RecurringBillingStatus.Pending);
        chase.MembershipSuspended.ShouldBeFalse(); // one decline must not read as a lockout
    }

    [Fact]
    public async Task A_membership_suspended_for_non_payment_is_flagged_on_its_dunning_row()
    {
        /*
         * "Locked out today" is derived from two facts, because there is no flag for it.
         * ApplyFailureAsync suspends by setting the membership to Frozen once retries are exhausted
         * and deliberately writes no freeze window — so the state it leaves is exactly
         * Abandoned + Frozen. Reading the attempt status alone would keep telling staff a member was
         * suspended after they had already been reinstated, which is the version of this figure that
         * costs a phone call to a paid-up member.
         */
        var ctx = await SeedGymAsync();

        var suspended = await SeedDunningAttemptAsync(ctx, "Locked", RecurringBillingStatus.Abandoned,
            failedAttempts: BillingRetryPolicy.MaxAttempts, failureReason: "Insufficient funds.",
            membershipStatus: MemberMembershipStatus.Frozen);
        var reinstated = await SeedDunningAttemptAsync(ctx, "Reinstated", RecurringBillingStatus.Abandoned,
            failedAttempts: BillingRetryPolicy.MaxAttempts, failureReason: "Insufficient funds.",
            membershipStatus: MemberMembershipStatus.Active);

        var rows = await SendAsync(new GetDunningAttemptsQuery());

        rows.Single(r => r.Id == suspended).MembershipSuspended.ShouldBeTrue();
        rows.Single(r => r.Id == reinstated).MembershipSuspended.ShouldBeFalse();
    }

    // ---------------------------------------------------------------------------------------
    // 2. Voiding an invoice
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Voiding_an_invoice_that_has_been_paid_is_refused()
    {
        /*
         * The refusal is the feature. Cancelling an invoice that money has landed against would
         * erase the debt while the payment row survives — the member's money would exist with
         * nothing on the books to explain it, and the day's takings would stop reconciling against
         * the invoices they came from. That case already has a correct verb: refund, then void.
         */
        var ctx = await SeedGymAsync();
        var invoiceId = await SeedInvoiceAsync(ctx, total: 130m);
        await SendAsync(new RecordPaymentCommand(invoiceId, PaymentMethod.Cash, 30m));

        var ex = await Should.ThrowAsync<ValidationException>(
            () => SendAsync(new VoidInvoiceCommand(invoiceId, "Raised twice by mistake")));

        ex.Message.ShouldContain("cannot be voided");
        ex.Message.ShouldContain("Refund the payment first");

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var invoice = await db.Invoices.IgnoreQueryFilters().SingleAsync(i => i.Id == invoiceId);

        invoice.Status.ShouldNotBe(InvoiceStatus.Cancelled); // the debt is still real
    }

    [Fact]
    public async Task Voiding_an_unpaid_invoice_cancels_it_and_records_why()
    {
        // The duplicate that sat in the overdue queue permanently inflating "outstanding" now has a
        // way out, and leaves behind the one thing that makes the void auditable months later: the
        // reason, appended to whatever the invoice already said about itself.
        var ctx = await SeedGymAsync();
        var invoiceId = await SeedInvoiceAsync(ctx, total: 130m, notes: "Auto-renewal — Standard Monthly");

        await SendAsync(new VoidInvoiceCommand(invoiceId, "Duplicate of INV-2026-000041"));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var invoice = await db.Invoices.IgnoreQueryFilters().SingleAsync(i => i.Id == invoiceId);

        invoice.Status.ShouldBe(InvoiceStatus.Cancelled);
        invoice.Notes.ShouldNotBeNull();
        invoice.Notes!.ShouldContain("Auto-renewal — Standard Monthly"); // context kept, not overwritten
        invoice.Notes!.ShouldContain("Duplicate of INV-2026-000041");
    }

    [Fact]
    public async Task Voiding_an_invoice_twice_is_refused_rather_than_re_stamped()
    {
        // Cancelled is terminal. A second void would append a second reason and rewrite the record
        // of when the invoice actually left the books, for no gain.
        var ctx = await SeedGymAsync();
        var invoiceId = await SeedInvoiceAsync(ctx, total: 60m);

        await SendAsync(new VoidInvoiceCommand(invoiceId, "Wrong member"));

        var ex = await Should.ThrowAsync<ValidationException>(
            () => SendAsync(new VoidInvoiceCommand(invoiceId, "Wrong member again")));

        ex.Message.ShouldContain("already been voided");

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var invoice = await db.Invoices.IgnoreQueryFilters().SingleAsync(i => i.Id == invoiceId);

        invoice.Notes.ShouldNotBeNull();
        invoice.Notes!.ShouldNotContain("Wrong member again");
    }

    // ---------------------------------------------------------------------------------------
    // 3. The coupon kill switch
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Deactivating_a_coupon_stops_it_discounting_the_next_renewal()
    {
        /*
         * Driven through the REAL redemption path, not by reading the flag back.
         *
         * Coupon.IsActive and Coupon.IsRedeemable both already existed and RenewMembershipCommand
         * already honoured them — the missing piece was any way for an owner to set the flag, so a
         * code that leaked onto a deals forum kept discounting every renewal until somebody reached
         * for psql. Asserting on the flag would pass even if nothing consumed it; asserting that the
         * renewal now fails is the only version of this test that pins the leak closed.
         */
        var ctx = await SeedGymAsync();
        var (couponId, code) = await SeedCouponAsync(ctx, percentOff: 20m);

        // It works first — otherwise the refusal below would prove nothing about the switch.
        await SendAsync(new RenewMembershipCommand(
            ctx.MemberId, ctx.PlanId, new DateOnly(2026, 1, 15), AutoRenew: false, CouponCode: code));

        await SendAsync(new SetCouponActiveCommand(couponId, IsActive: false));

        var ex = await Should.ThrowAsync<ValidationException>(
            () => SendAsync(new RenewMembershipCommand(
                ctx.MemberId, ctx.PlanId, new DateOnly(2026, 2, 15), AutoRenew: false, CouponCode: code)));

        ex.Message.ShouldContain(code);
        ex.Message.ShouldContain("not valid");

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var coupon = await db.Coupons.IgnoreQueryFilters().SingleAsync(c => c.Id == couponId);

        // The redemption that really happened is left alone: turning off a code is a decision about
        // the future, and rewriting TimesRedeemed would turn it into a lie about the past.
        coupon.TimesRedeemed.ShouldBe(1);
    }

    [Fact]
    public async Task Reactivating_a_coupon_makes_it_redeemable_again()
    {
        // The switch has to go both ways, or an owner who kills a code during a pricing scare has
        // traded one database edit for another.
        var ctx = await SeedGymAsync();
        var (couponId, code) = await SeedCouponAsync(ctx, percentOff: 20m);

        await SendAsync(new SetCouponActiveCommand(couponId, IsActive: false));
        await SendAsync(new SetCouponActiveCommand(couponId, IsActive: true));

        var membershipId = await SendAsync(new RenewMembershipCommand(
            ctx.MemberId, ctx.PlanId, new DateOnly(2026, 1, 15), AutoRenew: false, CouponCode: code));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var membership = await db.MemberMemberships.IgnoreQueryFilters().SingleAsync(m => m.Id == membershipId);

        membership.PricePaid.ShouldBe(120m); // 150 less 20%
    }

    [Fact]
    public async Task Deactivating_a_discount_withdraws_it_AND_stops_its_coupons_redeeming()
    {
        /*
         * Two effects, and the second one is the point.
         *
         * Withdrawing it from the catalogue is the easy half — GetDiscountsQuery hides inactive
         * rows, so it stops being offered. But redemption used to be gated on the COUPON's flag
         * alone, so every live code pointing at a switched-off discount kept discounting: the one
         * control an owner reaches for after an offer leaks left the offer running.
         *
         * RenewMembershipCommand now checks the discount's own flag as well. Deliberately NOT a
         * cascade onto the coupon rows — that would destroy the information needed to undo it,
         * because reactivating could not tell which coupons were switched off on purpose.
         */
        var ctx = await SeedGymAsync();
        var (_, code, discountId) = await SeedCouponWithDiscountAsync(ctx, percentOff: 50m);

        await SendAsync(new SetDiscountActiveCommand(discountId, IsActive: false));

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            var discount = await db.Discounts.IgnoreQueryFilters().SingleAsync(d => d.Id == discountId);
            discount.IsActive.ShouldBeFalse();
        }

        // The coupon itself is untouched and still "redeemable" on its own terms — the refusal has
        // to come from the discount behind it, which is exactly the hole this closes.
        var refused = await Should.ThrowAsync<FluentValidation.ValidationException>(async () =>
            await SendAsync(new RenewMembershipCommand(
                ctx.MemberId, ctx.PlanId, new DateOnly(2026, 1, 15), AutoRenew: false, CouponCode: code)));
        refused.Message.ShouldContain(code);
    }

    // ---------------------------------------------------------------------------------------
    // 4. Month-to-date revenue
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Month_to_date_revenue_is_net_of_completed_refunds()
    {
        /*
         * Completed payments MINUS completed refunds — the arithmetic InvoiceStatusPolicy,
         * GetInvoicesQuery and RecurringBillingJob all already use.
         *
         * This codebase has already been bitten once by two screens reading "revenue" differently:
         * the invoice list summed payments and stopped there while the detail subtracted refunds, so
         * the same invoice reported different money depending on which screen you were looking at. A
         * dashboard headline that summed gross would be the third reading, and the most visible one.
         */
        var ctx = await SeedGymAsync(withBillingPermission: true);
        var invoiceId = await SeedInvoiceAsync(ctx, total: 1000m);

        await SeedPaymentAsync(invoiceId, 200m, new DateTimeOffset(2026, 1, 5, 10, 0, 0, TimeSpan.Zero),
            refund: (50m, new DateTimeOffset(2026, 1, 12, 10, 0, 0, TimeSpan.Zero), RefundStatus.Completed));
        await SeedPaymentAsync(invoiceId, 100m, new DateTimeOffset(2026, 1, 10, 10, 0, 0, TimeSpan.Zero));

        // Neither of these is money that moved: a pending payment has not been taken, and a rejected
        // refund was never given back. Counting either would make the headline wrong in a direction
        // nobody would question.
        await SeedPaymentAsync(invoiceId, 999m, new DateTimeOffset(2026, 1, 8, 10, 0, 0, TimeSpan.Zero),
            status: PaymentStatus.Pending);
        await SeedPaymentAsync(invoiceId, 70m, new DateTimeOffset(2026, 1, 9, 10, 0, 0, TimeSpan.Zero),
            refund: (70m, new DateTimeOffset(2026, 1, 11, 10, 0, 0, TimeSpan.Zero), RefundStatus.Rejected));

        var summary = await SendAsync(new GetDashboardSummaryQuery(null));

        // 370 taken (200 + 100 + 70 — the pending 999 never moved), less the 50 actually given back.
        // The rejected 70 refund is not subtracted: it was never paid out.
        summary.RevenueThisMonth.ShouldBe(320m);
    }

    [Fact]
    public async Task Last_month_is_measured_over_the_same_span_so_the_comparison_is_like_for_like()
    {
        /*
         * The clock is 15 January, so "this month" is fifteen days and "last month" must be the
         * first fifteen days of December — not all thirty-one.
         *
         * Compared against a whole previous month, a healthy gym would show revenue collapsing every
         * single day of every month until roughly the 28th, and then recovering. That is a lie
         * manufactured by the comparison rather than read from the data, and a dashboard headline is
         * exactly the place it would be believed.
         */
        var ctx = await SeedGymAsync(withBillingPermission: true);
        var invoiceId = await SeedInvoiceAsync(ctx, total: 5000m);

        await SeedPaymentAsync(invoiceId, 400m, new DateTimeOffset(2025, 12, 3, 10, 0, 0, TimeSpan.Zero));
        // Inside December, outside the first fifteen days — real money, wrong span.
        await SeedPaymentAsync(invoiceId, 1000m, new DateTimeOffset(2025, 12, 20, 10, 0, 0, TimeSpan.Zero));
        await SeedPaymentAsync(invoiceId, 250m, new DateTimeOffset(2026, 1, 6, 10, 0, 0, TimeSpan.Zero));

        var summary = await SendAsync(new GetDashboardSummaryQuery(null));

        summary.RevenueThisMonth.ShouldBe(250m);
        summary.RevenueLastMonth.ShouldBe(400m);
    }

    [Fact]
    public async Task The_month_figures_are_hidden_from_a_caller_who_may_not_see_money()
    {
        /*
         * Null, not zero, and gated on billing.view rather than dashboard.view — the same treatment
         * todayRevenue already gets, for the same reason. Every staff role holds dashboard.view,
         * including Trainer, Nutritionist and Maintenance, none of which has a reason to read the
         * gym's takings. A new money field that inherited the wrong gate would reopen exactly the
         * hole that one was closed to fix.
         */
        var ctx = await SeedGymAsync(withBillingPermission: false);
        var invoiceId = await SeedInvoiceAsync(ctx, total: 500m);
        await SeedPaymentAsync(invoiceId, 500m, new DateTimeOffset(2026, 1, 5, 10, 0, 0, TimeSpan.Zero));

        var summary = await SendAsync(new GetDashboardSummaryQuery(null));

        summary.RevenueThisMonth.ShouldBeNull();
        summary.RevenueLastMonth.ShouldBeNull();
        summary.ActiveMembersCount.ShouldBe(1); // the rest of the dashboard still works
    }

    // ---------------------------------------------------------------------------------------
    // Seeding
    // ---------------------------------------------------------------------------------------

    private sealed record GymContext(Guid TenantId, Guid BranchId, Guid UserId, Guid MemberId, Guid PlanId);

    private async Task<GymContext> SeedGymAsync(bool withBillingPermission = false)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);

        var branch = new Branch { TenantId = tenant.Id, Name = "Main", AddressLine = "1 Main St", City = "City", Country = "US" };
        db.Branches.Add(branch);

        // AuditLog.UserId has an FK to Users, and BranchAccessResolver reads UserBranchAccess — the
        // acting user has to be a real row for either to work.
        var user = new User
        {
            TenantId = tenant.Id,
            Email = $"{Guid.NewGuid():N}@example.com",
            PasswordHash = "unused-in-this-test",
            FirstName = "Front",
            LastName = "Desk"
        };
        db.Users.Add(user);
        db.UserBranchAccesses.Add(new UserBranchAccess { UserId = user.Id, BranchId = branch.Id });

        var member = new Member
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12],
            FirstName = "Paying",
            LastName = "Member",
            Email = $"{Guid.NewGuid():N}@example.com",
            JoinDate = new DateOnly(2025, 1, 1),
            Status = MemberStatus.Active,
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
            IsActive = true
        };
        db.MembershipPlans.Add(plan);

        await db.SaveChangesAsync();

        CurrentUser.TenantId = tenant.Id;
        CurrentUser.UserId = user.Id;
        CurrentUser.IsAuthenticated = true;
        CurrentUser.Permissions = withBillingPermission ? [PermissionCodes.Billing.View] : [];

        return new GymContext(tenant.Id, branch.Id, user.Id, member.Id, plan.Id);
    }

    private async Task<Guid> SeedInvoiceAsync(GymContext ctx, decimal total, string? notes = null)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var invoice = new Invoice
        {
            TenantId = ctx.TenantId,
            BranchId = ctx.BranchId,
            MemberId = ctx.MemberId,
            InvoiceNumber = $"INV-{Guid.NewGuid():N}"[..12],
            IssueDate = new DateOnly(2026, 1, 5),
            DueDate = new DateOnly(2026, 1, 19),
            Status = InvoiceStatus.Issued,
            Subtotal = total,
            TotalAmount = total,
            Currency = "USD",
            Notes = notes
        };
        db.Invoices.Add(invoice);

        await db.SaveChangesAsync();
        return invoice.Id;
    }

    private async Task SeedPaymentAsync(
        Guid invoiceId, decimal amount, DateTimeOffset paidAt,
        PaymentStatus status = PaymentStatus.Completed,
        (decimal Amount, DateTimeOffset RefundedAt, RefundStatus Status)? refund = null)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var payment = new Payment
        {
            InvoiceId = invoiceId,
            Amount = amount,
            Method = PaymentMethod.Card,
            Status = status,
            PaidAt = paidAt
        };
        db.Payments.Add(payment);

        if (refund is { } r)
        {
            db.Refunds.Add(new Refund
            {
                Payment = payment,
                Amount = r.Amount,
                Reason = "Test refund",
                RefundedAt = r.RefundedAt,
                Status = r.Status
            });
        }

        await db.SaveChangesAsync();
    }

    private async Task<(Guid CouponId, string Code)> SeedCouponAsync(GymContext ctx, decimal percentOff)
    {
        var (couponId, code, _) = await SeedCouponWithDiscountAsync(ctx, percentOff);
        return (couponId, code);
    }

    private async Task<(Guid CouponId, string Code, Guid DiscountId)> SeedCouponWithDiscountAsync(
        GymContext ctx, decimal percentOff)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var discount = new Discount
        {
            TenantId = ctx.TenantId,
            MembershipPlanId = ctx.PlanId,
            Name = $"{percentOff}% off",
            Type = DiscountType.Percentage,
            Value = percentOff,
            IsActive = true
        };
        db.Discounts.Add(discount);

        var code = $"SAVE{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        db.Coupons.Add(new Coupon { TenantId = ctx.TenantId, Code = code, DiscountId = discount.Id, IsActive = true });

        await db.SaveChangesAsync();

        var coupon = await db.Coupons.IgnoreQueryFilters().SingleAsync(c => c.Code == code);
        return (coupon.Id, code, discount.Id);
    }

    /// <summary>
    /// One dunning row and everything it points at — the job builds member, membership and invoice
    /// together, and the query projects all three, so a partial seed would test a shape that cannot
    /// occur in production.
    /// </summary>
    private async Task<Guid> SeedDunningAttemptAsync(
        GymContext ctx, string memberFirstName, RecurringBillingStatus status, int failedAttempts,
        string? failureReason, MemberMembershipStatus membershipStatus = MemberMembershipStatus.Active)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var member = new Member
        {
            TenantId = ctx.TenantId,
            BranchId = ctx.BranchId,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12],
            FirstName = memberFirstName,
            LastName = "Member",
            Email = $"{Guid.NewGuid():N}@example.com",
            JoinDate = new DateOnly(2025, 1, 1),
            Status = MemberStatus.Active,
            QrCodeToken = Guid.NewGuid().ToString("N")
        };
        db.Members.Add(member);

        var membership = new MemberMembership
        {
            MemberId = member.Id,
            MembershipPlanId = ctx.PlanId,
            StartDate = new DateOnly(2025, 12, 15),
            EndDate = new DateOnly(2026, 1, 14),
            Status = membershipStatus,
            AutoRenew = true,
            PricePaid = 150m,
            Currency = "USD"
        };
        db.MemberMemberships.Add(membership);

        var invoice = new Invoice
        {
            TenantId = ctx.TenantId,
            BranchId = ctx.BranchId,
            MemberId = member.Id,
            InvoiceNumber = $"INV-{Guid.NewGuid():N}"[..12],
            IssueDate = new DateOnly(2026, 1, 14),
            DueDate = new DateOnly(2026, 1, 21),
            Status = InvoiceStatus.Issued,
            Subtotal = 150m,
            TotalAmount = 150m,
            Currency = "USD",
            Notes = "Auto-renewal — Standard Monthly"
        };
        db.Invoices.Add(invoice);

        var attempt = new RecurringBillingAttempt
        {
            TenantId = ctx.TenantId,
            BranchId = ctx.BranchId,
            MemberMembershipId = membership.Id,
            MemberId = member.Id,
            InvoiceId = invoice.Id,
            Status = status,
            FailedAttempts = failedAttempts,
            NextAttemptDate = new DateOnly(2026, 1, 16),
            LastAttemptDate = new DateOnly(2026, 1, 15),
            LastFailureReason = failureReason,
            Amount = 150m,
            Currency = "USD"
        };
        db.RecurringBillingAttempts.Add(attempt);

        await db.SaveChangesAsync();
        return attempt.Id;
    }
}
