using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GymOS.Api.IntegrationTests.TestSupport;
using GymOS.Application.Modules.Auth.Commands;
using GymOS.Application.Modules.Auth.Dtos;
using GymOS.Domain.Billing;
using GymOS.Domain.Identity;
using GymOS.Domain.Members;
using GymOS.Domain.Tenancy;
using GymOS.Infrastructure.Persistence;
using GymOS.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Api.IntegrationTests;

/// <summary>
/// The overpayment ceiling under simultaneous requests — the one thing the ceiling's own unit tests
/// structurally cannot check.
///
/// PaymentCeilingTests proves the rule, six ways, and every one of them is sequential: send, await,
/// send, await. That suite passed for the entire time the invariant was broken, because the failure
/// needs two requests in flight at once. Six concurrent full payments against a $100 invoice were all
/// accepted, leaving $600 recorded and the balance at -$500; eight concurrent refunds took $800 back
/// out of a $100 payment. Issued one after another the same requests are correctly refused, which is
/// what makes it a race and not a logic error.
///
/// It lives here rather than beside the other billing tests for a reason that is not preference: the
/// Application suite runs on SQLite in-memory over a single shared connection, which serialises every
/// write by construction and has no row locking to exercise. It cannot fail this test, so it cannot
/// prove it either. This project runs against real PostgreSQL, which is the only place the defect and
/// its fix both exist.
///
/// These go through the real HTTP pipeline rather than calling the handler, because the fix depends on
/// TransactionBehavior having opened the transaction the row lock lives in. A handler invoked directly
/// would hold no transaction and the lock would be released immediately — passing for a reason that
/// has nothing to do with production.
/// </summary>
public class PaymentConcurrencyTests(GymOsWebApplicationFactory factory) : IClassFixture<GymOsWebApplicationFactory>
{
    private const decimal InvoiceTotal = 100m;

    /// <summary>Enough overlap to lose the race reliably; the original bug reproduced at six.</summary>
    private const int Simultaneous = 6;

    [Fact]
    public async Task Six_simultaneous_full_payments_settle_the_invoice_exactly_once()
    {
        var (email, invoiceId) = await SeedInvoiceAsync();
        var token = await LoginAsync(email);

        var responses = await FireTogetherAsync(
            Simultaneous,
            client => client.PostAsJsonAsync(
                $"/api/invoices/{invoiceId}/payments",
                new { method = nameof(PaymentMethod.Cash), amount = InvoiceTotal }),
            token);

        // Exactly one may win. The rest must be refused by the ceiling, not by an error — a 500 here
        // would mean the lock turned a wrong answer into a broken one.
        responses.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        responses.Where(r => r.StatusCode != HttpStatusCode.OK)
            .ShouldAllBe(r => r.StatusCode == HttpStatusCode.BadRequest);

        var (paid, status) = await ReadInvoiceAsync(invoiceId);
        paid.ShouldBe(InvoiceTotal);
        status.ShouldBe(InvoiceStatus.Paid);
    }

    [Fact]
    public async Task Simultaneous_part_payments_never_sum_past_the_total()
    {
        // The subtler shape: each request is individually legitimate, and only their sum is not. A
        // fix that merely rejected identical duplicates would let all six of these through.
        var (email, invoiceId) = await SeedInvoiceAsync();
        var token = await LoginAsync(email);

        var responses = await FireTogetherAsync(
            Simultaneous,
            client => client.PostAsJsonAsync(
                $"/api/invoices/{invoiceId}/payments",
                new { method = nameof(PaymentMethod.Cash), amount = 40m }),
            token);

        // 40 + 40 fits in 100; the third is refused with 20 left owing, and so is everything after it.
        responses.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(2);

        var (paid, status) = await ReadInvoiceAsync(invoiceId);
        paid.ShouldBe(80m);
        status.ShouldBe(InvoiceStatus.PartiallyPaid);
    }

    [Fact]
    public async Task Simultaneous_refunds_cannot_take_back_more_than_was_paid()
    {
        var (email, invoiceId) = await SeedInvoiceAsync();
        var token = await LoginAsync(email);

        var paymentResponse = await SendAsync(
            token, c => c.PostAsJsonAsync(
                $"/api/invoices/{invoiceId}/payments",
                new { method = nameof(PaymentMethod.Cash), amount = InvoiceTotal }));
        paymentResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var paymentId = await paymentResponse.Content.ReadFromJsonAsync<Guid>();

        var responses = await FireTogetherAsync(
            Simultaneous,
            client => client.PostAsJsonAsync(
                $"/api/invoices/payments/{paymentId}/refund",
                new { amount = InvoiceTotal, reason = "Concurrency probe" }),
            token);

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(1);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var refunded = await db.Refunds.IgnoreQueryFilters()
            .Where(r => r.PaymentId == paymentId && r.Status == RefundStatus.Completed)
            .Select(r => r.Amount)
            .ToListAsync();

        refunded.Sum().ShouldBe(InvoiceTotal);
    }

    [Fact]
    public async Task The_database_itself_refuses_an_overpayment_even_with_the_application_bypassed()
    {
        // Written directly against the tables, with no handler, no validator and no ceiling in the
        // way — which is exactly the position a future fifth payment writer would be in if whoever
        // added it did not know the rule. The guard has to hold for code that has not been written
        // yet, and the only place that can is the database.
        var (email, invoiceId) = await SeedInvoiceAsync();
        var token = await LoginAsync(email);

        var ok = await SendAsync(
            token, c => c.PostAsJsonAsync(
                $"/api/invoices/{invoiceId}/payments",
                new { method = nameof(PaymentMethod.Cash), amount = InvoiceTotal }));
        ok.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var ex = await Should.ThrowAsync<DbUpdateException>(async () =>
        {
            db.Payments.Add(new Payment
            {
                InvoiceId = invoiceId,
                Method = PaymentMethod.Cash,
                Amount = 1m,
                PaidAt = DateTimeOffset.UtcNow,
                Status = PaymentStatus.Completed
            });
            await db.SaveChangesAsync();
        });

        ex.InnerException?.Message.ShouldContain("exceeds its total");

        var (paid, _) = await ReadInvoiceAsync(invoiceId);
        paid.ShouldBe(InvoiceTotal);
    }

    /// <summary>
    /// Releases every request at the same instant.
    ///
    /// Building the clients first and gating them on one TaskCompletionSource is what makes this a
    /// real overlap. Awaiting each in turn — or even Task.WhenAll over tasks that were started as
    /// they were created — lets the first finish before the last begins on a fast machine, and the
    /// test then passes against the unfixed code.
    /// </summary>
    private async Task<IReadOnlyList<HttpResponseMessage>> FireTogetherAsync(
        int count, Func<HttpClient, Task<HttpResponseMessage>> send, string token)
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var inFlight = Enumerable.Range(0, count).Select(async _ =>
        {
            var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            await gate.Task;
            return await send(client);
        }).ToList();

        gate.SetResult();
        return await Task.WhenAll(inFlight);
    }

    private async Task<HttpResponseMessage> SendAsync(string token, Func<HttpClient, Task<HttpResponseMessage>> send)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await send(client);
    }

    private async Task<(decimal Paid, InvoiceStatus Status)> ReadInvoiceAsync(Guid invoiceId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var invoice = await db.Invoices.IgnoreQueryFilters().SingleAsync(i => i.Id == invoiceId);
        var amounts = await db.Payments.IgnoreQueryFilters()
            .Where(p => p.InvoiceId == invoiceId && p.Status == PaymentStatus.Completed)
            .Select(p => p.Amount)
            .ToListAsync();

        return (amounts.Sum(), invoice.Status);
    }

    /// <summary>Its own tenant per test, so the shared test database cannot make one test's
    /// concurrency show up in another's totals.</summary>
    private async Task<(string Email, Guid InvoiceId)> SeedInvoiceAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);

        var branch = new Branch { TenantId = tenant.Id, Name = "Main", AddressLine = "1 Main St", City = "City", Country = "US" };
        db.Branches.Add(branch);

        var email = $"{Guid.NewGuid():N}@example.com";
        var user = new User
        {
            TenantId = tenant.Id,
            Email = email,
            PasswordHash = new GymOS.Infrastructure.Identity.PasswordHasher().Hash(TestDataSeeder.Password),
            FirstName = "Front",
            LastName = "Desk",
            IsActive = true
        };
        db.Users.Add(user);
        db.UserBranchAccesses.Add(new UserBranchAccess { UserId = user.Id, BranchId = branch.Id });

        var role = new Role { TenantId = tenant.Id, Name = $"Role-{Guid.NewGuid():N}" };
        db.Roles.Add(role);
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });

        foreach (var code in new[] { PermissionCodes.Billing.RecordPayment, PermissionCodes.Billing.IssueRefund })
        {
            var permission = await db.Permissions.FirstOrDefaultAsync(p => p.Code == code)
                ?? new Permission { Code = code, Module = code.Split('.')[0], Description = code };
            if (db.Entry(permission).State == EntityState.Detached)
            {
                db.Permissions.Add(permission);
            }
            db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
        }

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
            IssueDate = DateOnly.FromDateTime(DateTime.UtcNow),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            Status = InvoiceStatus.Issued,
            Subtotal = InvoiceTotal,
            TotalAmount = InvoiceTotal,
            Currency = "USD"
        };
        db.Invoices.Add(invoice);

        await db.SaveChangesAsync();
        return (email, invoice.Id);
    }

    private async Task<string> LoginAsync(string email)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginCommand(email, TestDataSeeder.Password, null));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResultDto>())!.AccessToken;
    }
}
