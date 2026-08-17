using GymOS.Application.Common.Exceptions;
using GymOS.Application.Modules.Crm.Queries;
using GymOS.Application.Modules.Members.Commands;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Identity;
using GymOS.Domain.Members;
using GymOS.Domain.Tenancy;
using GymOS.Infrastructure.Persistence;
using GymOS.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Members;

/// <summary>
/// The referral chain's two rules: an attributed referrer must actually exist in the caller's
/// tenant (a bogus or cross-tenant id is rejected, not silently stored), and the top-referrers
/// leaderboard counts and orders correctly.
/// </summary>
public class MemberReferralTests : ApplicationTestBase
{
    [Fact]
    public async Task Creating_a_member_with_a_referrer_stores_the_attribution()
    {
        var (tenantId, branchId, userId, referrerId) = await SeedAsync();
        SetAuthenticatedAs(tenantId, userId);

        var created = await SendAsync(new CreateMemberCommand(
            "Ria", "Novak", $"{Guid.NewGuid():N}@example.com", null, null, null, null, branchId, referrerId));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        (await db.Members.IgnoreQueryFilters().SingleAsync(m => m.Id == created.Id))
            .ReferredByMemberId.ShouldBe(referrerId);
    }

    [Fact]
    public async Task A_nonexistent_referrer_id_is_rejected()
    {
        var (tenantId, branchId, userId, _) = await SeedAsync();
        SetAuthenticatedAs(tenantId, userId);

        await Should.ThrowAsync<NotFoundException>(() => SendAsync(new CreateMemberCommand(
            "Ria", "Novak", $"{Guid.NewGuid():N}@example.com", null, null, null, null, branchId, Guid.NewGuid())));
    }

    [Fact]
    public async Task A_referrer_from_another_tenant_is_rejected()
    {
        var (_, _, _, foreignReferrerId) = await SeedAsync();
        var (tenantId, branchId, userId, _) = await SeedAsync();
        SetAuthenticatedAs(tenantId, userId);

        // The other tenant's member id is real, but the tenant query filter must make it invisible.
        await Should.ThrowAsync<NotFoundException>(() => SendAsync(new CreateMemberCommand(
            "Ria", "Novak", $"{Guid.NewGuid():N}@example.com", null, null, null, null, branchId, foreignReferrerId)));
    }

    [Fact]
    public async Task Top_referrers_orders_by_referral_count()
    {
        var (tenantId, branchId, userId, bigReferrerId) = await SeedAsync();
        SetAuthenticatedAs(tenantId, userId);

        using (var seedScope = CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            var smallReferrer = NewMember(tenantId, branchId, "Sasha", "Lee");
            db.Members.Add(smallReferrer);
            db.Members.Add(NewMember(tenantId, branchId, "Ref", "One", bigReferrerId));
            db.Members.Add(NewMember(tenantId, branchId, "Ref", "Two", bigReferrerId));
            db.Members.Add(NewMember(tenantId, branchId, "Ref", "Three", smallReferrer.Id));
            await db.SaveChangesAsync();
        }

        var top = await SendAsync(new GetTopReferrersQuery(BranchId: null));

        top.Count.ShouldBe(2);
        top[0].MemberId.ShouldBe(bigReferrerId);
        top[0].ReferralCount.ShouldBe(2);
        top[1].ReferralCount.ShouldBe(1);
        top[1].FullName.ShouldBe("Sasha Lee");
    }

    private static Member NewMember(Guid tenantId, Guid branchId, string first, string last, Guid? referredBy = null) => new()
    {
        TenantId = tenantId,
        BranchId = branchId,
        MemberCode = $"MBR-{Guid.NewGuid():N}"[..12],
        FirstName = first,
        LastName = last,
        Email = $"{Guid.NewGuid():N}@example.com",
        JoinDate = DateOnly.FromDateTime(DateTime.UtcNow),
        QrCodeToken = Guid.NewGuid().ToString("N"),
        ReferredByMemberId = referredBy
    };

    private void SetAuthenticatedAs(Guid tenantId, Guid userId)
    {
        CurrentUser.TenantId = tenantId;
        CurrentUser.UserId = userId;
        CurrentUser.IsAuthenticated = true;
    }

    private async Task<(Guid TenantId, Guid BranchId, Guid UserId, Guid ReferrerId)> SeedAsync()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);

        var branch = new Branch { TenantId = tenant.Id, Name = "Main", AddressLine = "1 Main St", City = "City", Country = "US" };
        db.Branches.Add(branch);

        // CreateMemberCommand now provisions a portal login alongside the Member row, and that
        // requires the tenant's Member role to already exist — exactly as a real, seeded tenant has.
        db.Roles.Add(new Role { TenantId = tenant.Id, Name = RoleNames.Member, IsSystemRole = true });

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

        var referrer = NewMember(tenant.Id, branch.Id, "Rae", "Ortiz");
        db.Members.Add(referrer);

        await db.SaveChangesAsync();
        return (tenant.Id, branch.Id, staffUser.Id, referrer.Id);
    }
}
