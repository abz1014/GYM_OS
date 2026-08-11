using FluentValidation;
using GymOS.Application.Modules.Billing.Commands;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Billing;
using GymOS.Domain.Identity;
using GymOS.Domain.Members;
using GymOS.Domain.Tenancy;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Billing;

/// <summary>
/// The one invariant that protects the money: a payment may never exceed what is still owed.
///
/// The rule was written but never tested, which for an invariant about money is close to not having
/// it — nothing would fail if a refactor dropped the check, and the symptom (revenue figures that
/// are wrong rather than absent) is the kind nobody notices until a month-end.
///
/// It also carries the weight of double-submit. There is no idempotency key here on purpose: paying
/// twice is something a person can legitimately do — two £20 cash payments against a £40 invoice are
/// two real payments — so matching on "looks like a duplicate" would refuse honest money. What is
/// never legitimate is paying MORE than is owed, and a double-submit is exactly that. The tests
/// below pin both halves: the duplicate is refused, and the two genuine partials are not.
/// </summary>
public class PaymentCeilingTests : ApplicationTestBase
{
    [Fact]
    public async Task A_payment_larger_than_the_balance_is_refused()
    {
        var s = await SeedInvoiceAsync(100m);

        var ex = await Should.ThrowAsync<ValidationException>(
            () => SendAsync(new RecordPaymentCommand(s.InvoiceId, PaymentMethod.Cash, 100_000m)));

        ex.Message.ShouldContain("exceeds");
        (await CompletedPaymentTotalAsync(s.InvoiceId)).ShouldBe(0m);
    }

    [Fact]
    public async Task Paying_an_already_settled_invoice_is_refused()
    {
        var s = await SeedInvoiceAsync(100m);
        await SendAsync(new RecordPaymentCommand(s.InvoiceId, PaymentMethod.Cash, 100m));

        var ex = await Should.ThrowAsync<ValidationException>(
            () => SendAsync(new RecordPaymentCommand(s.InvoiceId, PaymentMethod.Cash, 100m)));

        ex.Message.ShouldContain("already settled");
        // The invoice keeps exactly one payment: the second submit added nothing.
        (await CompletedPaymentTotalAsync(s.InvoiceId)).ShouldBe(100m);
    }

    [Fact]
    public async Task A_double_submit_of_the_full_amount_cannot_pay_the_invoice_twice()
    {
        var s = await SeedInvoiceAsync(49.99m);

        await SendAsync(new RecordPaymentCommand(s.InvoiceId, PaymentMethod.Cash, 49.99m));
        await Should.ThrowAsync<ValidationException>(
            () => SendAsync(new RecordPaymentCommand(s.InvoiceId, PaymentMethod.Cash, 49.99m)));

        (await CompletedPaymentTotalAsync(s.InvoiceId)).ShouldBe(49.99m);
    }

    [Fact]
    public async Task Two_genuine_part_payments_of_the_same_amount_both_stand()
    {
        // The other half of the rule above. An idempotency key keyed on "same invoice, same amount"
        // would have refused this, and it is an ordinary way for a member to settle a bill.
        var s = await SeedInvoiceAsync(40m);

        await SendAsync(new RecordPaymentCommand(s.InvoiceId, PaymentMethod.Cash, 20m));
        await SendAsync(new RecordPaymentCommand(s.InvoiceId, PaymentMethod.Cash, 20m));

        (await CompletedPaymentTotalAsync(s.InvoiceId)).ShouldBe(40m);

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        (await db.Invoices.IgnoreQueryFilters().SingleAsync(i => i.Id == s.InvoiceId))
            .Status.ShouldBe(InvoiceStatus.Paid);
    }

    [Fact]
    public async Task An_overpayment_by_card_never_reaches_the_processor()
    {
        // Ordering, not outcome. If the ceiling were checked after the charge the member's card
        // would be debited and the payment then refused — the worst of both. Only the count can
        // tell those two orderings apart, which is why the fake gateway keeps one.
        var s = await SeedInvoiceAsync(50m);

        await Should.ThrowAsync<ValidationException>(
            () => SendAsync(new RecordPaymentCommand(s.InvoiceId, PaymentMethod.Card, 5_000m)));

        PaymentGateway.ChargeCount.ShouldBe(0);
        (await CompletedPaymentTotalAsync(s.InvoiceId)).ShouldBe(0m);
    }

    [Fact]
    public async Task A_declined_card_records_no_payment()
    {
        var s = await SeedInvoiceAsync(50m);
        PaymentGateway.Succeeds = false;

        await Should.ThrowAsync<ValidationException>(
            () => SendAsync(new RecordPaymentCommand(s.InvoiceId, PaymentMethod.Card, 50m)));

        PaymentGateway.ChargeCount.ShouldBe(1);
        (await CompletedPaymentTotalAsync(s.InvoiceId)).ShouldBe(0m);
    }

    private async Task<decimal> CompletedPaymentTotalAsync(Guid invoiceId)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var rows = await db.Payments.IgnoreQueryFilters()
            .Where(p => p.InvoiceId == invoiceId && p.Status == PaymentStatus.Completed)
            .Select(p => p.Amount)
            .ToListAsync();
        return rows.Sum();
    }

    private async Task<(Guid TenantId, Guid InvoiceId)> SeedInvoiceAsync(decimal total)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);

        var branch = new Branch { TenantId = tenant.Id, Name = "Main", AddressLine = "1 Main St", City = "City", Country = "US" };
        db.Branches.Add(branch);

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
            JoinDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = MemberStatus.Active,
            QrCodeToken = Guid.NewGuid().ToString("N")
        };
        db.Members.Add(member);

        var invoice = new Invoice
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            MemberId = member.Id,
            InvoiceNumber = $"INV-{Guid.NewGuid():N}"[..12],
            IssueDate = new DateOnly(2026, 1, 10),
            DueDate = new DateOnly(2026, 1, 24),
            Status = InvoiceStatus.Issued,
            Subtotal = total,
            TotalAmount = total,
            Currency = "USD"
        };
        db.Invoices.Add(invoice);

        await db.SaveChangesAsync();

        CurrentUser.TenantId = tenant.Id;
        CurrentUser.UserId = user.Id;
        CurrentUser.IsAuthenticated = true;

        return (tenant.Id, invoice.Id);
    }
}
