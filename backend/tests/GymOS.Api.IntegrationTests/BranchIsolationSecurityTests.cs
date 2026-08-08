using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GymOS.Api.IntegrationTests.TestSupport;
using GymOS.Application.Modules.Auth.Commands;
using GymOS.Application.Modules.Auth.Dtos;
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
/// Regression coverage for a second cross-boundary exposure found during a staff-to-staff
/// security review: UserBranchAccess was populated at login only to drive the frontend's branch
/// switcher, never enforced server-side. A staff user (Receptionist) seeded with access to
/// exactly one branch could read another branch's full member list by supplying its id directly,
/// or every branch's members by omitting the filter entirely — confirmed live (307 -> the actual
/// full seeded set, vs. the ~100 in their own branch). Fixed with BranchScopeBehavior (rejects an
/// explicit foreign BranchId for any request carrying one) plus restricting each affected list
/// query's "no BranchId given" fallback to the caller's own UserBranchAccess rows.
/// </summary>
public class BranchIsolationSecurityTests(GymOsWebApplicationFactory factory) : IClassFixture<GymOsWebApplicationFactory>
{
    [Fact]
    public async Task Staff_with_one_branch_never_sees_another_branchs_members_with_no_filter()
    {
        var (tenantId, branchAId, branchBId, staffEmail) = await SeedTwoBranchTenantAsync();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            db.Members.Add(NewMember(tenantId, branchAId, "InBranchA"));
            db.Members.Add(NewMember(tenantId, branchBId, "InBranchB"));
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(client, staffEmail));

        var response = await client.GetAsync("/api/members?page=1&pageSize=50");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<MemberPage>();

        page!.items.ShouldContain(m => m.fullName.Contains("InBranchA"));
        page.items.ShouldNotContain(m => m.fullName.Contains("InBranchB"));
    }

    [Fact]
    public async Task Staff_explicitly_requesting_a_foreign_branch_gets_403()
    {
        var (_, _, branchBId, staffEmail) = await SeedTwoBranchTenantAsync();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(client, staffEmail));

        var response = await client.GetAsync($"/api/members?branchId={branchBId}&page=1&pageSize=10");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Staff_requesting_their_own_branch_explicitly_still_works()
    {
        var (tenantId, branchAId, _, staffEmail) = await SeedTwoBranchTenantAsync();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            db.Members.Add(NewMember(tenantId, branchAId, "OwnBranchMember"));
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(client, staffEmail));

        var response = await client.GetAsync($"/api/members?branchId={branchAId}&page=1&pageSize=10");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<MemberPage>();
        page!.items.ShouldContain(m => m.fullName.Contains("OwnBranchMember"));
    }

    /*
     * The three tests above all exercise the LIST. That was the shape of the original finding, and
     * it is exactly why the next gap survived them: BranchScopeBehavior can only inspect a property
     * literally named BranchId on the REQUEST, so a request that names an entity by its own id had
     * nothing for it to check. Confirmed live before the fix — a Receptionist scoped to one branch
     * could not see another branch's member in the list, and could read that same member's name,
     * date of birth and home address by fetching them by id.
     *
     * These two cover reading and writing by id, which is where a list-shaped test will never look.
     */

    [Fact]
    public async Task Staff_cannot_read_a_foreign_branch_member_by_id()
    {
        var (tenantId, _, branchBId, staffEmail) = await SeedTwoBranchTenantAsync();

        Guid foreignMemberId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            var foreign = NewMember(tenantId, branchBId, "ForeignByIdRead");
            db.Members.Add(foreign);
            await db.SaveChangesAsync();
            foreignMemberId = foreign.Id;
        }

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(client, staffEmail));

        var response = await client.GetAsync($"/api/members/{foreignMemberId}");

        // NotFound, not Forbidden: a 403 would confirm the id exists in a branch they cannot see.
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Staff_cannot_modify_a_foreign_branch_member_by_id()
    {
        var (tenantId, _, branchBId, staffEmail) = await SeedTwoBranchTenantAsync();

        Guid foreignMemberId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            var foreign = NewMember(tenantId, branchBId, "ForeignByIdWrite");
            db.Members.Add(foreign);
            await db.SaveChangesAsync();
            foreignMemberId = foreign.Id;
        }

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(client, staffEmail));

        var response = await client.PutAsJsonAsync($"/api/members/{foreignMemberId}", new
        {
            id = foreignMemberId,
            firstName = "Overwritten",
            lastName = "ByForeignStaff",
            email = "overwritten@example.com",
            phone = "555-0100",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // The status alone is not the assertion that matters — the row must be untouched.
        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var stored = await verifyDb.Members.IgnoreQueryFilters().FirstAsync(m => m.Id == foreignMemberId);
        stored.LastName.ShouldBe("ForeignByIdWrite");
    }

    private static Member NewMember(Guid tenantId, Guid branchId, string lastName) => new()
    {
        TenantId = tenantId,
        BranchId = branchId,
        MemberCode = $"MBR-{Guid.NewGuid():N}"[..12],
        FirstName = "Test",
        LastName = lastName,
        Email = $"{Guid.NewGuid():N}@example.com",
        JoinDate = DateOnly.FromDateTime(DateTime.UtcNow),
        QrCodeToken = Guid.NewGuid().ToString("N")
    };

    private async Task<(Guid TenantId, Guid BranchAId, Guid BranchBId, string StaffEmail)> SeedTwoBranchTenantAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);

        var branchA = new Branch { TenantId = tenant.Id, Name = "Branch A", AddressLine = "1 A St", City = "City", Country = "US" };
        var branchB = new Branch { TenantId = tenant.Id, Name = "Branch B", AddressLine = "1 B St", City = "City", Country = "US" };
        db.Branches.AddRange(branchA, branchB);

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

        // Access to Branch A only — this is the exact seeding shape the real demo Receptionist has.
        db.UserBranchAccesses.Add(new UserBranchAccess { UserId = user.Id, BranchId = branchA.Id });

        var role = new Role { TenantId = tenant.Id, Name = $"Role-{Guid.NewGuid():N}" };
        db.Roles.Add(role);
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });

        /*
         * View AND update. Update matters: without it the write test 403s at the permission policy
         * and never reaches the branch check, so it would pass while proving nothing — which is
         * exactly what it did on first run. A security test that cannot reach the code it guards is
         * worse than no test, because it reads like coverage.
         */
        foreach (var code in new[] { PermissionCodes.Members.View, PermissionCodes.Members.Update })
        {
            var permission = await db.Permissions.FirstOrDefaultAsync(p => p.Code == code)
                ?? new Permission { Code = code, Module = "members", Description = code };
            if (db.Entry(permission).State == EntityState.Detached)
            {
                db.Permissions.Add(permission);
            }
            db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
        }

        await db.SaveChangesAsync();
        return (tenant.Id, branchA.Id, branchB.Id, email);
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginCommand(email, TestDataSeeder.Password, null));
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AuthResultDto>();
        return result!.AccessToken;
    }

    private record MemberPage(List<MemberRow> items);

    private record MemberRow(string fullName);
}
