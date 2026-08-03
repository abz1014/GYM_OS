using GymOS.Application.Modules.Equipment.Commands;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Identity;
using GymOS.Domain.Tenancy;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Equipment;

/// <summary>
/// CreateAssetCommand's core business rule: AssetTag is a sequential "EQ-0001" style number scoped
/// per tenant. A shared/global counter would either collide across tenants or leak how many assets
/// another tenant owns through the next tag issued — the same risk class as the branch-isolation
/// bugs found in this session's security review, just one level up (tenant instead of branch).
/// </summary>
public class CreateAssetCommandHandlerTests : ApplicationTestBase
{
    [Fact]
    public async Task Asset_tags_are_sequential_within_a_tenant()
    {
        var (tenantId, branchId, userId) = await SeedTenantAsync();
        SetAuthenticatedAs(tenantId, userId);

        var firstId = await SendAsync(new CreateAssetCommand("Treadmill", "Cardio", branchId, null, null, null, null, null, null));
        var secondId = await SendAsync(new CreateAssetCommand("Rowing Machine", "Cardio", branchId, null, null, null, null, null, null));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        (await db.Assets.SingleAsync(a => a.Id == firstId)).AssetTag.ShouldBe("EQ-0001");
        (await db.Assets.SingleAsync(a => a.Id == secondId)).AssetTag.ShouldBe("EQ-0002");
    }

    [Fact]
    public async Task Asset_tag_numbering_restarts_for_a_different_tenant()
    {
        var (tenantAId, branchAId, userAId) = await SeedTenantAsync();
        SetAuthenticatedAs(tenantAId, userAId);
        await SendAsync(new CreateAssetCommand("Treadmill", "Cardio", branchAId, null, null, null, null, null, null));

        var (tenantBId, branchBId, userBId) = await SeedTenantAsync();
        SetAuthenticatedAs(tenantBId, userBId);
        var tenantBAssetId = await SendAsync(new CreateAssetCommand("Squat Rack", "Strength", branchBId, null, null, null, null, null, null));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        (await db.Assets.SingleAsync(a => a.Id == tenantBAssetId)).AssetTag.ShouldBe("EQ-0001");
    }

    private void SetAuthenticatedAs(Guid tenantId, Guid userId)
    {
        CurrentUser.TenantId = tenantId;
        CurrentUser.UserId = userId;
        CurrentUser.IsAuthenticated = true;
    }

    private async Task<(Guid TenantId, Guid BranchId, Guid UserId)> SeedTenantAsync()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);

        var branch = new Branch { TenantId = tenant.Id, Name = "Main", AddressLine = "1 Main St", City = "City", Country = "US" };
        db.Branches.Add(branch);

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

        await db.SaveChangesAsync();
        return (tenant.Id, branch.Id, staffUser.Id);
    }
}
