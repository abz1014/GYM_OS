using FluentValidation;
using GymOS.Application.Common.Interfaces;
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
/// Member login: the account behind the Member row.
///
/// Before this, CreateMemberCommand wrote a Member and nothing else — Member.UserId stayed null
/// forever, so a member registered through the real "Register Member" button could never sign in to
/// any of the portal work the rest of this product was built to hand them. These tests pin the two
/// paths that close that: a brand-new member gets a working login the moment they're registered, and
/// a member who predates the feature (or lost their password) gets one from a single retrofit action
/// that never creates a second account for the same person.
/// </summary>
public class MemberLoginTests : ApplicationTestBase
{
    [Fact]
    public async Task Registering_a_member_returns_a_login_that_works_and_grants_the_Member_role_and_branch_access()
    {
        var gym = await SeedGymAsync();
        SignInAs(gym, gym.OwnerId);

        var result = await SendAsync(new CreateMemberCommand(
            "New", "Member", "new.member@example.com", null, null, null, null, gym.DowntownBranchId));

        result.TemporaryPassword.ShouldNotBeNullOrWhiteSpace();

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var member = await db.Members.IgnoreQueryFilters().SingleAsync(m => m.Id == result.Id);
        member.UserId.ShouldNotBeNull();

        var user = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == member.UserId);
        user.Email.ShouldBe("new.member@example.com");
        user.IsActive.ShouldBeTrue();
        passwordHasher.Verify(result.TemporaryPassword, user.PasswordHash).ShouldBeTrue();

        var roleIds = await db.UserRoles.Where(ur => ur.UserId == user.Id).Select(ur => ur.RoleId).ToListAsync();
        roleIds.ShouldHaveSingleItem();
        roleIds[0].ShouldBe(gym.RoleIds[RoleNames.Member]);

        var branchIds = await db.UserBranchAccesses.Where(uba => uba.UserId == user.Id).Select(uba => uba.BranchId).ToListAsync();
        branchIds.ShouldHaveSingleItem();
        branchIds[0].ShouldBe(gym.DowntownBranchId);
    }

    /// <summary>
    /// LoginCommand resolves an email with no tenant context at sign-in time, so a tenant-scoped
    /// uniqueness check would let two different accounts share an address and strand the second one
    /// with a login that could never resolve. Checked against the gym's own staff account here, which
    /// is the case a tenant-scoped check would have let straight through.
    /// </summary>
    [Fact]
    public async Task Email_uniqueness_is_checked_globally_not_per_tenant()
    {
        var gym = await SeedGymAsync();
        SignInAs(gym, gym.OwnerId);

        await Should.ThrowAsync<ValidationException>(() => SendAsync(new CreateMemberCommand(
            "Dup", "Licate", gym.OwnerEmail, null, null, null, null, gym.DowntownBranchId)));
    }

    [Fact]
    public async Task An_existing_loginless_member_can_be_given_a_first_login()
    {
        var gym = await SeedGymAsync();
        SignInAs(gym, gym.OwnerId);

        var memberId = await SeedLoginlessMemberAsync(gym, "legacy.member@example.com");

        var result = await SendAsync(new ProvisionMemberLoginCommand(memberId));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var member = await db.Members.IgnoreQueryFilters().SingleAsync(m => m.Id == memberId);
        member.UserId.ShouldNotBeNull();

        var user = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == member.UserId);
        user.Email.ShouldBe("legacy.member@example.com");
        passwordHasher.Verify(result.TemporaryPassword, user.PasswordHash).ShouldBeTrue();
    }

    /// <summary>
    /// The same action, pressed a second time (or pressed by someone who forgot the member already
    /// has a login) must not fork a second User for one Member — it resets the existing account, the
    /// same handover ResetStaffPasswordCommand gives staff, including revoking whatever sessions the
    /// old password had open.
    /// </summary>
    [Fact]
    public async Task A_member_who_already_has_a_login_gets_a_password_reset_instead_of_a_second_account()
    {
        var gym = await SeedGymAsync();
        SignInAs(gym, gym.OwnerId);

        var created = await SendAsync(new CreateMemberCommand(
            "Reset", "Me", "reset.me@example.com", null, null, null, null, gym.DowntownBranchId));

        Guid originalUserId;
        using (var seedScope = CreateScope())
        {
            var seedDb = seedScope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            var member = await seedDb.Members.IgnoreQueryFilters().SingleAsync(m => m.Id == created.Id);
            originalUserId = member.UserId!.Value;

            seedDb.RefreshTokens.Add(new RefreshToken
            {
                UserId = originalUserId,
                TokenHash = "live-session-token-hash",
                CreatedAt = DateTimeProvider.UtcNow,
                ExpiresAt = DateTimeProvider.UtcNow.AddDays(7)
            });
            await seedDb.SaveChangesAsync();
        }

        var result = await SendAsync(new ProvisionMemberLoginCommand(created.Id));
        result.Id.ShouldBe(created.Id);

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var member2 = await db.Members.IgnoreQueryFilters().SingleAsync(m => m.Id == created.Id);
        member2.UserId.ShouldBe(originalUserId);

        // Exactly one User ever existed for this email — the reset reused the account, it did not fork one.
        (await db.Users.IgnoreQueryFilters().CountAsync(u => u.Email == "reset.me@example.com")).ShouldBe(1);

        var user = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == originalUserId);
        passwordHasher.Verify(result.TemporaryPassword, user.PasswordHash).ShouldBeTrue();
        passwordHasher.Verify(created.TemporaryPassword, user.PasswordHash).ShouldBeFalse();

        var tokens = await db.RefreshTokens.IgnoreQueryFilters().Where(t => t.UserId == originalUserId).ToListAsync();
        tokens.ShouldAllBe(t => t.RevokedAt != null);
    }

    private sealed record SeededGym(
        Guid TenantId, Guid DowntownBranchId, Guid OwnerId, string OwnerEmail, Dictionary<string, Guid> RoleIds);

    private void SignInAs(SeededGym gym, Guid userId)
    {
        CurrentUser.TenantId = gym.TenantId;
        CurrentUser.UserId = userId;
        CurrentUser.IsAuthenticated = true;
    }

    private async Task<SeededGym> SeedGymAsync()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Gym-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N"), IsActive = true };
        db.Tenants.Add(tenant);

        var downtown = new Branch { TenantId = tenant.Id, Name = "Downtown", City = "Metropolis", Country = "United States", IsActive = true };
        db.Branches.Add(downtown);

        var roles = RoleNames.All.ToDictionary(
            name => name, name => new Role { TenantId = tenant.Id, Name = name, IsSystemRole = true });
        db.Roles.AddRange(roles.Values);

        var ownerEmail = $"owner-{Guid.NewGuid():N}@titan.example.com";
        var owner = new User
        {
            TenantId = tenant.Id,
            Email = ownerEmail,
            PasswordHash = "seeded-hash",
            FirstName = "Owner",
            LastName = "Account",
            IsActive = true
        };
        db.Users.Add(owner);
        db.UserRoles.Add(new UserRole { UserId = owner.Id, RoleId = roles[RoleNames.Owner].Id });
        db.UserBranchAccesses.Add(new UserBranchAccess { UserId = owner.Id, BranchId = downtown.Id });

        await db.SaveChangesAsync();

        return new SeededGym(tenant.Id, downtown.Id, owner.Id, ownerEmail, roles.ToDictionary(r => r.Key, r => r.Value.Id));
    }

    private async Task<Guid> SeedLoginlessMemberAsync(SeededGym gym, string email)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var member = new Member
        {
            TenantId = gym.TenantId,
            BranchId = gym.DowntownBranchId,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12],
            FirstName = "Legacy",
            LastName = "Member",
            Email = email,
            JoinDate = DateOnly.FromDateTime(DateTime.UtcNow),
            QrCodeToken = Guid.NewGuid().ToString("N")
        };
        db.Members.Add(member);
        await db.SaveChangesAsync();
        return member.Id;
    }
}
