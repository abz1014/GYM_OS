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
/// Who may read the gym's takings off the dashboard.
///
/// dashboard.view is seeded to EVERY staff role, including Trainer, Nutritionist and Maintenance —
/// none of which has a reason to see revenue. /api/reports/revenue already refused those roles, so
/// the same figure was blocked at one door and served at the other. Confirmed live against the
/// deployed API: a Trainer token read todayRevenue 1199.98 from /api/dashboard/summary while its
/// own call to /api/reports/revenue returned 403.
///
/// The two money fields are now gated on billing.view and come back null — not zero — to everyone
/// else. Zero would be indistinguishable from a real quiet morning, and a caller cannot tell a
/// withheld number from a true one.
///
/// This lives in the integration suite rather than beside the other handler tests on purpose: the
/// dashboard summary combines the global branch query filter with its own accessible-branch filter,
/// and that double list-Contains does not translate on the SQLite harness the Application tests use.
/// It needs real Postgres to run at all.
/// </summary>
public class DashboardRevenueVisibilityTests(GymOsWebApplicationFactory factory) : IClassFixture<GymOsWebApplicationFactory>
{
    private const decimal PaymentAmount = 120m;

    [Fact]
    public async Task Staff_without_billing_view_get_no_revenue_but_keep_the_rest_of_the_dashboard()
    {
        // A Trainer's real seeded grant: dashboard, members, their roster — and no billing.
        var email = await SeedGymAsync(PermissionCodes.Dashboard.View, PermissionCodes.Members.View);

        var summary = await GetSummaryAsync(email);

        summary.todayRevenue.ShouldBeNull();
        summary.todayCashCollected.ShouldBeNull();

        // The rest of the screen still works — this is a redaction, not a broken endpoint.
        summary.activeMembersCount.ShouldBe(1);
    }

    [Fact]
    public async Task Staff_with_billing_view_still_get_the_real_figures()
    {
        var email = await SeedGymAsync(PermissionCodes.Dashboard.View, PermissionCodes.Billing.View);

        var summary = await GetSummaryAsync(email);

        summary.todayRevenue.ShouldBe(PaymentAmount);
        // The seeded payment is by card, so this zero is a real split, not a withheld figure.
        summary.todayCashCollected.ShouldBe(0m);
        summary.activeMembersCount.ShouldBe(1);
    }

    private async Task<Summary> GetSummaryAsync(string email)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await LoginAsync(client, email));

        var response = await client.GetAsync("/api/dashboard/summary");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<Summary>())!;
    }

    /// <summary>
    /// A fresh single-branch tenant with one active member and one completed card payment taken
    /// today, plus a staff login holding exactly the permissions given. Its own tenant, so the
    /// tenant-scoped totals below can be asserted exactly despite the shared test database.
    /// </summary>
    private async Task<string> SeedGymAsync(params string[] permissionCodes)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);

        var branch = new Branch
        {
            TenantId = tenant.Id, Name = "Main", AddressLine = "1 Main St", City = "City", Country = "US"
        };
        db.Branches.Add(branch);

        var email = $"{Guid.NewGuid():N}@example.com";
        var user = new User
        {
            TenantId = tenant.Id,
            Email = email,
            PasswordHash = new GymOS.Infrastructure.Identity.PasswordHasher().Hash(TestDataSeeder.Password),
            FirstName = "Staff",
            LastName = "User",
            IsActive = true
        };
        db.Users.Add(user);

        // Without this the accessible-branch set is empty and every count comes back 0, which would
        // make the redaction test pass for the wrong reason.
        db.UserBranchAccesses.Add(new UserBranchAccess { UserId = user.Id, BranchId = branch.Id });

        var role = new Role { TenantId = tenant.Id, Name = $"Role-{Guid.NewGuid():N}" };
        db.Roles.Add(role);
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });

        foreach (var code in permissionCodes)
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
            Status = InvoiceStatus.Paid,
            Subtotal = PaymentAmount,
            TotalAmount = PaymentAmount,
            Currency = "USD"
        };
        invoice.Payments.Add(new Payment
        {
            Method = PaymentMethod.Card,
            Amount = PaymentAmount,
            // "Today" for the dashboard is the real clock here, so anchor to now rather than a date.
            PaidAt = DateTimeOffset.UtcNow,
            Status = PaymentStatus.Completed
        });
        db.Invoices.Add(invoice);

        await db.SaveChangesAsync();
        return email;
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginCommand(email, TestDataSeeder.Password, null));
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AuthResultDto>();
        return result!.AccessToken;
    }

    private record Summary(decimal? todayRevenue, decimal? todayCashCollected, int activeMembersCount);
}
