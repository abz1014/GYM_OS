using GymOS.Application.Modules.Members.Queries;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Identity;
using GymOS.Domain.Members;
using GymOS.Domain.Tenancy;
using GymOS.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Members;

/// <summary>
/// Finding a member by the token on their card.
///
/// Every member has carried a QrCodeToken since the day they were created, and the check-in method
/// is literally called QrSimulated — but nothing ever searched by it, so a real scanner emitting a
/// token found nobody. The front desk has one input for a name, a code, an email and a card, because
/// that is the only shape that works when the thing typing is a barcode reader.
/// </summary>
public class MemberQrLookupTests : ApplicationTestBase
{
    [Fact]
    public async Task A_scanned_token_finds_exactly_that_member()
    {
        var ctx = await SeedAsync();
        AsStaff(ctx);

        var result = await SendAsync(new GetMembersListQuery(ctx.ScannedToken, null, null));

        result.Items.ShouldHaveSingleItem().Id.ShouldBe(ctx.ScannedMemberId);
    }

    [Fact]
    public async Task The_same_box_still_finds_people_by_name_code_and_email()
    {
        // Adding the token must not cost the desk the search it already had.
        var ctx = await SeedAsync();
        AsStaff(ctx);

        (await SendAsync(new GetMembersListQuery("Aria", null, null)))
            .Items.ShouldHaveSingleItem().Id.ShouldBe(ctx.ScannedMemberId);
        (await SendAsync(new GetMembersListQuery(ctx.ScannedMemberCode, null, null)))
            .Items.ShouldHaveSingleItem().Id.ShouldBe(ctx.ScannedMemberId);
        (await SendAsync(new GetMembersListQuery(ctx.ScannedEmail, null, null)))
            .Items.ShouldHaveSingleItem().Id.ShouldBe(ctx.ScannedMemberId);
    }

    [Fact]
    public async Task Half_a_token_matches_nobody()
    {
        // Exact, not substring. A token is opaque and nobody types one by hand, so a partial match
        // buys nothing — while a short stray term colliding with the middle of somebody's token would
        // hand the desk a member they were never looking for.
        var ctx = await SeedAsync();
        AsStaff(ctx);

        var half = ctx.ScannedToken[..16];

        (await SendAsync(new GetMembersListQuery(half, null, null))).Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_token_from_another_gym_finds_nobody()
    {
        // The tenant filter still owns this: a token is a bearer-ish string on a card, and one gym's
        // card must never resolve inside another gym's console.
        var ctx = await SeedAsync();
        var other = await SeedAsync();
        AsStaff(ctx);

        (await SendAsync(new GetMembersListQuery(other.ScannedToken, null, null))).Items.ShouldBeEmpty();
    }

    private void AsStaff(SeedContext ctx)
    {
        CurrentUser.TenantId = ctx.TenantId;
        CurrentUser.UserId = ctx.StaffUserId;
        CurrentUser.IsAuthenticated = true;
    }

    private record SeedContext(
        Guid TenantId, Guid StaffUserId, Guid ScannedMemberId, string ScannedToken, string ScannedMemberCode, string ScannedEmail);

    private async Task<SeedContext> SeedAsync()
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

        var scanned = new Member
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12],
            FirstName = "Aria",
            LastName = "First",
            Email = $"{Guid.NewGuid():N}@example.com",
            JoinDate = new DateOnly(2025, 1, 1),
            QrCodeToken = Guid.NewGuid().ToString("N")
        };
        db.Members.Add(scanned);

        // A second member, so "found exactly one" is a real result rather than the only row present.
        db.Members.Add(new Member
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12],
            FirstName = "Belle",
            LastName = "Second",
            Email = $"{Guid.NewGuid():N}@example.com",
            JoinDate = new DateOnly(2025, 1, 1),
            QrCodeToken = Guid.NewGuid().ToString("N")
        });

        await db.SaveChangesAsync();
        return new SeedContext(tenant.Id, staffUser.Id, scanned.Id, scanned.QrCodeToken, scanned.MemberCode, scanned.Email);
    }
}
