using GymOS.Application.Common.Interfaces;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Billing;
using GymOS.Domain.Members;
using GymOS.Domain.Memberships;
using GymOS.Domain.Tenancy;
using GymOS.Infrastructure.BackgroundJobs;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace GymOS.Application.Tests.Billing;

/// <summary>
/// The auto-renewal engine, driven against the real DbContext. The headline case is two
/// memberships renewing on the SAME day: an earlier version numbered both invoices from a single
/// COUNT(*) and violated the per-tenant unique invoice-number index — which would have broken
/// billing for any gym with more than one renewal a day (i.e. all of them). Found by running the
/// job against seeded data, pinned here so it can't come back.
/// </summary>
public class RecurringBillingJobTests : ApplicationTestBase
{
    [Fact]
    public async Task Renewing_two_memberships_on_the_same_day_issues_distinct_invoice_numbers()
    {
        var ctx = await SeedAsync(dueMemberships: 2);

        var created = await RunJobAsync(alwaysSucceed: true);

        created.ShouldBeGreaterThan(0);

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var renewalInvoices = await db.Invoices.IgnoreQueryFilters()
            .Where(i => i.TenantId == ctx.TenantId && i.Notes!.StartsWith("Auto-renewal"))
            .ToListAsync();

        renewalInvoices.Count.ShouldBe(2);
        renewalInvoices.Select(i => i.InvoiceNumber).Distinct().Count().ShouldBe(2);
    }

    [Fact]
    public async Task A_successful_charge_pays_the_invoice_and_extends_the_membership()
    {
        var ctx = await SeedAsync(dueMemberships: 1);
        var originalEnd = ctx.OriginalEndDate;

        await RunJobAsync(alwaysSucceed: true);

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var attempt = await db.RecurringBillingAttempts.IgnoreQueryFilters().SingleAsync();
        attempt.Status.ShouldBe(RecurringBillingStatus.Succeeded);

        var invoice = await db.Invoices.IgnoreQueryFilters().SingleAsync(i => i.Id == attempt.InvoiceId);
        invoice.Status.ShouldBe(InvoiceStatus.Paid);

        var membership = await db.MemberMemberships.IgnoreQueryFilters().SingleAsync(m => m.Id == attempt.MemberMembershipId);
        membership.EndDate.ShouldBeGreaterThan(originalEnd);
        membership.Status.ShouldBe(MemberMembershipStatus.Active);
    }

    [Fact]
    public async Task A_declined_charge_schedules_a_retry_rather_than_suspending_immediately()
    {
        await SeedAsync(dueMemberships: 1);

        await RunJobAsync(alwaysSucceed: false);

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var attempt = await db.RecurringBillingAttempts.IgnoreQueryFilters().SingleAsync();
        attempt.Status.ShouldBe(RecurringBillingStatus.Pending);
        attempt.FailedAttempts.ShouldBe(1);
        attempt.LastFailureReason.ShouldNotBeNullOrEmpty();

        // Still active — one decline must not cost a paying member their access.
        var membership = await db.MemberMemberships.IgnoreQueryFilters().SingleAsync(m => m.Id == attempt.MemberMembershipId);
        membership.Status.ShouldBe(MemberMembershipStatus.Active);
    }

    [Fact]
    public async Task Exhausting_every_retry_abandons_the_attempt_and_freezes_the_membership()
    {
        await SeedAsync(dueMemberships: 1);

        // Re-run past the retry budget, advancing the clock so each scheduled retry comes due.
        for (var i = 0; i < BillingRetryPolicy.MaxAttempts; i++)
        {
            await RunJobAsync(alwaysSucceed: false);
            DateTimeProvider.UtcNow = DateTimeProvider.UtcNow.AddDays(7);
        }

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var attempt = await db.RecurringBillingAttempts.IgnoreQueryFilters().SingleAsync();
        attempt.Status.ShouldBe(RecurringBillingStatus.Abandoned);
        attempt.FailedAttempts.ShouldBe(BillingRetryPolicy.MaxAttempts);

        var membership = await db.MemberMemberships.IgnoreQueryFilters().SingleAsync(m => m.Id == attempt.MemberMembershipId);
        membership.Status.ShouldBe(MemberMembershipStatus.Frozen);
    }

    [Fact]
    public async Task Re_running_the_job_does_not_double_bill_a_membership()
    {
        await SeedAsync(dueMemberships: 1);

        await RunJobAsync(alwaysSucceed: false); // leaves a Pending attempt
        await RunJobAsync(alwaysSucceed: false); // must reuse it, not raise a second invoice

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        (await db.RecurringBillingAttempts.IgnoreQueryFilters().CountAsync()).ShouldBe(1);
        (await db.Invoices.IgnoreQueryFilters().CountAsync(i => i.Notes!.StartsWith("Auto-renewal"))).ShouldBe(1);
    }

    [Fact]
    public async Task A_membership_cancelled_after_the_renewal_was_raised_is_neither_charged_nor_revived()
    {
        /*
         * Found adversarially, chasing the freeze-resurrection bug into the job: attempts were
         * selected by THEIR status and nothing re-read the membership's, so a membership cancelled
         * between raise and collection still had its card charged — and the success path then set it
         * back to Active, CancellationReason and all. A background job must not overrule a member's
         * decision to leave, and it especially must not bill them for doing so.
         */
        await SeedAsync(dueMemberships: 1);

        await RunJobAsync(alwaysSucceed: false); // raises the attempt, first charge declines

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            var membership = await db.MemberMemberships.IgnoreQueryFilters().SingleAsync();
            membership.Status = MemberMembershipStatus.Cancelled;
            membership.CancellationReason = "member quit";
            await db.SaveChangesAsync();
        }

        DateTimeProvider.UtcNow = DateTimeProvider.UtcNow.AddDays(7); // the retry comes due
        await RunJobAsync(alwaysSucceed: true);                       // and the card would work

        using var verify = CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var attempt = await verifyDb.RecurringBillingAttempts.IgnoreQueryFilters().SingleAsync();
        attempt.Status.ShouldBe(RecurringBillingStatus.Abandoned);

        (await verifyDb.Payments.IgnoreQueryFilters().CountAsync()).ShouldBe(0); // nothing left the card

        var after = await verifyDb.MemberMemberships.IgnoreQueryFilters().SingleAsync();
        after.Status.ShouldBe(MemberMembershipStatus.Cancelled);
        after.CancellationReason.ShouldBe("member quit");
    }

    [Fact]
    public async Task A_paid_renewal_extends_but_does_not_unfreeze_a_member_requested_freeze()
    {
        /*
         * The success path set Status = Active unconditionally, which stomped a member's real freeze
         * — window still on the row, nothing credited. A member-requested freeze is recognisable by
         * its window (the dunning suspension never sets one), so a paid renewal now extends the
         * membership and leaves the pause running.
         */
        await SeedAsync(dueMemberships: 1);

        await RunJobAsync(alwaysSucceed: false); // raises the attempt

        var today = DateOnly.FromDateTime(DateTimeProvider.UtcNow.UtcDateTime);
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            var membership = await db.MemberMemberships.IgnoreQueryFilters().SingleAsync();
            membership.Status = MemberMembershipStatus.Frozen; // the member froze in the meantime
            membership.FreezeStartDate = today;
            membership.FreezeEndDate = today.AddDays(14);
            await db.SaveChangesAsync();
        }

        DateTimeProvider.UtcNow = DateTimeProvider.UtcNow.AddDays(7);
        await RunJobAsync(alwaysSucceed: true);

        using var verify = CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var after = await verifyDb.MemberMemberships.IgnoreQueryFilters().SingleAsync();

        after.Status.ShouldBe(MemberMembershipStatus.Frozen); // the pause survives the payment
        after.FreezeStartDate.ShouldBe(today);                // window untouched, still creditable
        after.EndDate.ShouldBeGreaterThan(today);             // but the paid period was granted
    }

    [Fact]
    public async Task An_auto_renewal_grants_the_new_period_a_fresh_freeze_allowance()
    {
        // Manual renewal creates a new row whose FreezeDaysUsed is zero; auto-renewal reuses the
        // row, so without this reset the two paths quietly sold different products — one allowance
        // per period over the counter, one per lifetime on autopay.
        await SeedAsync(dueMemberships: 1);

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            var membership = await db.MemberMemberships.IgnoreQueryFilters().SingleAsync();
            membership.FreezeDaysUsed = 12; // spent during the period now ending
            await db.SaveChangesAsync();
        }

        await RunJobAsync(alwaysSucceed: true);

        using var verify = CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var after = await verifyDb.MemberMemberships.IgnoreQueryFilters().SingleAsync();

        after.Status.ShouldBe(MemberMembershipStatus.Active);
        after.FreezeDaysUsed.ShouldBe(0);
    }

    private async Task<int> RunJobAsync(bool alwaysSucceed)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var job = new RecurringBillingJob(
            db,
            new StubPaymentGateway(alwaysSucceed),
            DateTimeProvider,
            NullLogger<RecurringBillingJob>.Instance);

        return await job.RunAsync();
    }

    private async Task<(Guid TenantId, DateOnly OriginalEndDate)> SeedAsync(int dueMemberships)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);

        var branch = new Branch { TenantId = tenant.Id, Name = "Main", AddressLine = "1 Main St", City = "City", Country = "US" };
        db.Branches.Add(branch);

        var plan = new MembershipPlan
        {
            TenantId = tenant.Id,
            Name = "Monthly",
            Type = MembershipPlanType.Monthly,
            DurationDays = 30,
            Price = 49.99m,
            Currency = "USD",
            IsActive = true
        };
        db.MembershipPlans.Add(plan);

        var endDate = DateOnly.FromDateTime(DateTimeProvider.UtcNow.UtcDateTime);

        for (var i = 0; i < dueMemberships; i++)
        {
            var member = new Member
            {
                TenantId = tenant.Id,
                BranchId = branch.Id,
                MemberCode = $"MBR-{Guid.NewGuid():N}"[..12],
                FirstName = $"Renew{i}",
                LastName = "Test",
                Email = $"{Guid.NewGuid():N}@example.com",
                JoinDate = new DateOnly(2025, 1, 1),
                QrCodeToken = Guid.NewGuid().ToString("N"),
                Status = MemberStatus.Active
            };
            db.Members.Add(member);

            db.MemberMemberships.Add(new MemberMembership
            {
                MemberId = member.Id,
                MembershipPlanId = plan.Id,
                StartDate = endDate.AddDays(-30),
                EndDate = endDate,
                Status = MemberMembershipStatus.Active,
                AutoRenew = true,
                PricePaid = plan.Price,
                Currency = plan.Currency
            });
        }

        await db.SaveChangesAsync();
        return (tenant.Id, endDate);
    }

    /// <summary>Deterministic stand-in for the gateway so success and decline are both provable.</summary>
    private sealed class StubPaymentGateway(bool succeed) : IPaymentGateway
    {
        public Task<PaymentGatewayResult> ChargeAsync(decimal amount, string currency, string description, CancellationToken cancellationToken = default)
            => Task.FromResult(succeed
                ? new PaymentGatewayResult(true, $"TEST-{Guid.NewGuid():N}", null)
                : new PaymentGatewayResult(false, null, "Card declined."));

        public Task<PaymentGatewayResult> RefundAsync(string gatewayTransactionId, decimal amount, CancellationToken cancellationToken = default)
            => Task.FromResult(new PaymentGatewayResult(true, $"TEST-REFUND-{Guid.NewGuid():N}", null));
    }
}
