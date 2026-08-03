using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Members;
using GymOS.Domain.Tenancy;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Common;

/// <summary>
/// Non-Negotiable Principle #8 ("multi-tenancy is mandatory") lives entirely in
/// GymOsDbContext's global query filters — a bug here is a cross-tenant data leak, so it gets
/// tested directly against the real GymOsDbContext rather than assumed from a code read.
/// </summary>
public class TenantIsolationTests : ApplicationTestBase
{
    [Fact]
    public async Task Querying_as_tenant_A_never_returns_tenant_Bs_rows()
    {
        var (tenantA, branchA) = await SeedTenantAsync();
        var (tenantB, branchB) = await SeedTenantAsync();

        await SeedMemberAsync(tenantA.Id, branchA.Id, "alice@a.example.com");
        await SeedMemberAsync(tenantB.Id, branchB.Id, "bob@b.example.com");

        CurrentUser.TenantId = tenantA.Id;
        CurrentUser.IsAuthenticated = true;

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var visibleMembers = await db.Members.ToListAsync();

        visibleMembers.ShouldHaveSingleItem();
        visibleMembers[0].Email.ShouldBe("alice@a.example.com");
    }

    [Fact]
    public async Task Switching_tenant_context_switches_which_rows_are_visible()
    {
        var (tenantA, branchA) = await SeedTenantAsync();
        var (tenantB, branchB) = await SeedTenantAsync();

        await SeedMemberAsync(tenantA.Id, branchA.Id, "alice@a.example.com");
        await SeedMemberAsync(tenantB.Id, branchB.Id, "bob@b.example.com");

        CurrentUser.TenantId = tenantB.Id;
        CurrentUser.IsAuthenticated = true;

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var visibleMembers = await db.Members.ToListAsync();

        visibleMembers.ShouldHaveSingleItem();
        visibleMembers[0].Email.ShouldBe("bob@b.example.com");
    }

    [Fact]
    public async Task Soft_deleted_members_are_filtered_out_but_still_physically_present()
    {
        var (tenant, branch) = await SeedTenantAsync();
        var memberId = await SeedMemberAsync(tenant.Id, branch.Id, "carol@a.example.com");

        CurrentUser.TenantId = tenant.Id;
        CurrentUser.IsAuthenticated = true;

        using (var deleteScope = CreateScope())
        {
            var db = deleteScope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            var member = await db.Members.SingleAsync(m => m.Id == memberId);
            member.IsDeleted = true;
            await db.SaveChangesAsync();
        }

        using var queryScope = CreateScope();
        var queryDb = queryScope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        (await queryDb.Members.AnyAsync(m => m.Id == memberId)).ShouldBeFalse();
        (await queryDb.Members.IgnoreQueryFilters().AnyAsync(m => m.Id == memberId)).ShouldBeTrue();
    }

    private async Task<(Tenant Tenant, Branch Branch)> SeedTenantAsync()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);

        var branch = new Branch { TenantId = tenant.Id, Name = "Main", AddressLine = "1 Main St", City = "City", Country = "US" };
        db.Branches.Add(branch);

        await db.SaveChangesAsync();
        return (tenant, branch);
    }

    private async Task<Guid> SeedMemberAsync(Guid tenantId, Guid branchId, string email)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var member = new Member
        {
            TenantId = tenantId,
            BranchId = branchId,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12],
            FirstName = "Test",
            LastName = "Member",
            Email = email,
            JoinDate = DateOnly.FromDateTime(DateTimeProvider.UtcNow.UtcDateTime),
            QrCodeToken = Guid.NewGuid().ToString("N")
        };

        db.Members.Add(member);
        await db.SaveChangesAsync();
        return member.Id;
    }
}
