using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Modules.Settings.Commands;
using GymOS.Application.Modules.Settings.Queries;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Identity;
using GymOS.Domain.Tenancy;
using GymOS.Infrastructure.Persistence;
using GymOS.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Settings;

/// <summary>
/// Staff management: the screen a gym uses to hire, edit, stand down and re-admit its own employees.
///
/// Before these handlers existed there was no way to do any of it. Every staff account in the product
/// came out of the demo seeder, the only login the API could create was a trainer's, and that trainer
/// could then never be edited or switched off. Hiring a receptionist required a developer running a
/// CLI seed against the database — which is to say a gym could not hire anyone.
///
/// The rules pinned here are the ones where getting it wrong is not merely wrong but unrecoverable:
/// an account locked out with nobody left able to unlock it, or a stranger's user reachable by id.
/// </summary>
public class StaffManagementTests : ApplicationTestBase
{
    /// <summary>
    /// The temporary password is the entire handover — there is no mail sender in this product, so
    /// the manager reads the returned string out to the new hire and it is never recoverable again.
    /// A handler that returned a password it had not actually hashed onto the row (or hashed a
    /// different one) would look completely correct at the call site and be discovered only when a
    /// brand-new employee could not sign in on their first morning. So the assertion is not "a string
    /// came back" but "that exact string verifies against the stored hash".
    ///
    /// The UserRole and UserBranchAccess rows are checked in the same test because they are what make
    /// the account worth anything: without a role the person can reach no screen, and without branch
    /// access every branch-scoped query filters their data down to nothing. All three writes are one
    /// act of hiring.
    /// </summary>
    [Fact]
    public async Task Hiring_a_staff_member_returns_a_password_that_works_and_grants_role_and_branches()
    {
        var gym = await SeedGymAsync();
        SignInAs(gym, gym.OwnerId);

        var result = await SendAsync(new CreateStaffCommand(
            "newhire@titan.example.com", "New", "Hire", "+1-555-0142", RoleNames.Receptionist,
            [gym.DowntownBranchId, gym.UptownBranchId]));

        result.TemporaryPassword.ShouldNotBeNullOrWhiteSpace();

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var created = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == result.Id);
        created.Email.ShouldBe("newhire@titan.example.com");
        created.Phone.ShouldBe("+1-555-0142");
        created.IsActive.ShouldBeTrue();
        passwordHasher.Verify(result.TemporaryPassword, created.PasswordHash).ShouldBeTrue();

        var roleIds = await db.UserRoles.Where(ur => ur.UserId == result.Id).Select(ur => ur.RoleId).ToListAsync();
        roleIds.ShouldHaveSingleItem();
        roleIds[0].ShouldBe(gym.RoleIds[RoleNames.Receptionist]);

        var branchIds = await db.UserBranchAccesses.Where(uba => uba.UserId == result.Id).Select(uba => uba.BranchId).ToListAsync();
        branchIds.Count.ShouldBe(2);
        branchIds.ShouldContain(gym.DowntownBranchId);
        branchIds.ShouldContain(gym.UptownBranchId);
    }

    /// <summary>
    /// Deactivating yourself is an unrecoverable lockout, not an inconvenience.
    ///
    /// LoginCommand refuses inactive users and RefreshTokenCommand re-checks IsActive on every
    /// rotation, so the moment the flag flips the person is out — including out of this screen, which
    /// is the only place in the product that could switch them back on. There is no super-admin
    /// console above the tenant. A single mis-clicked row in the staff table would end with the gym
    /// phoning a developer to run an UPDATE statement.
    ///
    /// The message is asserted verbatim because it is the whole of the user's understanding of what
    /// just happened; a generic "operation failed" would read as a bug in the button.
    /// </summary>
    [Fact]
    public async Task Deactivating_your_own_account_is_refused()
    {
        var gym = await SeedGymAsync();
        SignInAs(gym, gym.OwnerId);

        var ex = await Should.ThrowAsync<ValidationException>(() => SendAsync(new DeactivateStaffCommand(gym.OwnerId)));
        ex.Message.ShouldBe("You cannot deactivate your own account.");

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        (await db.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == gym.OwnerId)).IsActive.ShouldBeTrue();
    }

    /// <summary>
    /// The same lockout, one step removed and therefore easier to walk into: a manager standing down
    /// the last colleague who can manage staff, from an account that cannot.
    ///
    /// Self-deactivation is the obvious case and it is not the dangerous one — nobody is surprised
    /// when the product stops them switching themselves off. This is the case where every individual
    /// click looks legitimate and the gym is left with a staff screen no living account can open.
    ///
    /// The check counts holders of settings.manage_staff by ROLE grant rather than by the caller's own
    /// token, because the permission matrix is editable at runtime: who can hire tomorrow is whatever
    /// the RolePermission rows say now, not what was true when anyone last signed in.
    /// </summary>
    [Fact]
    public async Task Deactivating_the_last_account_that_can_manage_staff_is_refused()
    {
        var gym = await SeedGymAsync();

        // A receptionist does the deactivating, so the self-deactivation rule cannot be what saves us
        // — the owner really is somebody else, and really is the last one who can hire.
        var receptionistId = await SeedUserAsync(gym, "front.desk@titan.example.com", RoleNames.Receptionist);
        SignInAs(gym, receptionistId);

        var ex = await Should.ThrowAsync<ValidationException>(() => SendAsync(new DeactivateStaffCommand(gym.OwnerId)));
        ex.Message.ShouldContain("last active account that can manage staff");

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        (await db.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == gym.OwnerId)).IsActive.ShouldBeTrue();
    }

    /// <summary>
    /// The counterpart: once a second holder exists the door is no longer one-way, and the product
    /// must not keep refusing. A rule that only ever says no is indistinguishable from a broken button.
    /// </summary>
    [Fact]
    public async Task Deactivating_a_staff_manager_is_allowed_once_another_one_exists()
    {
        var gym = await SeedGymAsync();
        var managerId = await SeedUserAsync(gym, "manager@titan.example.com", RoleNames.Manager);
        SignInAs(gym, managerId);

        await SendAsync(new DeactivateStaffCommand(gym.OwnerId));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        (await db.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == gym.OwnerId)).IsActive.ShouldBeFalse();
    }

    /// <summary>
    /// Members are not staff, and there are three orders of magnitude more of them.
    ///
    /// Users and members share one table by design — a member with a portal login is a User row like
    /// any other. A staff list built as "every user in the tenant" would therefore open on a page
    /// listing the entire membership, each one with a role dropdown and a deactivate button next to
    /// it. That is not a cosmetic problem: the screen's purpose disappears under the noise, and
    /// changing a member's row here would silently reassign their portal access.
    /// </summary>
    [Fact]
    public async Task The_staff_list_excludes_members_and_offers_only_staff_roles()
    {
        var gym = await SeedGymAsync();
        await SeedUserAsync(gym, "gym.goer@example.com", RoleNames.Member);
        var trainerId = await SeedUserAsync(gym, "coach@titan.example.com", RoleNames.Trainer);
        SignInAs(gym, gym.OwnerId);

        var result = await SendAsync(new GetStaffListQuery());

        result.Staff.Select(s => s.Email).ShouldNotContain("gym.goer@example.com");
        result.Staff.Select(s => s.Id).ShouldContain(gym.OwnerId);
        result.Staff.Select(s => s.Id).ShouldContain(trainerId);

        var trainer = result.Staff.First(s => s.Id == trainerId);
        trainer.RoleName.ShouldBe(RoleNames.Trainer);
        trainer.BranchIds.ShouldContain(gym.DowntownBranchId);

        // The role dropdown is built from this list, so Member appearing in it would put the excluded
        // role back within one click of every staff account.
        result.Roles.Select(r => r.Name).ShouldNotContain(RoleNames.Member);
        result.Roles.Select(r => r.Name).ShouldContain(RoleNames.Receptionist);
    }

    /// <summary>
    /// A user id from another gym must come back as "no such user", never as "not allowed".
    ///
    /// Guid ids are not guessable, but this endpoint takes one straight off the URL and the difference
    /// between 404 and 403 is the difference between silence and confirmation that the id names a real
    /// person somewhere. The isolation itself comes from the global tenant query filter — the handler
    /// loads through the filtered DbSet, so a foreign row is not found rather than found-and-refused,
    /// which is also what stops the write half from ever touching it.
    /// </summary>
    [Fact]
    public async Task Updating_a_user_from_another_tenant_is_a_not_found_not_a_forbidden()
    {
        var ourGym = await SeedGymAsync();
        var otherGym = await SeedGymAsync();

        SignInAs(ourGym, ourGym.OwnerId);

        await Should.ThrowAsync<NotFoundException>(() => SendAsync(new UpdateStaffCommand(
            otherGym.OwnerId, "Hijacked", "Name", null, RoleNames.Receptionist, [ourGym.DowntownBranchId])));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        (await db.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == otherGym.OwnerId)).FirstName.ShouldBe("Owner");
    }

    /// <summary>
    /// The same boundary on the branch side of the write. Passing a foreign branch id would otherwise
    /// hand one gym's employee standing access to another gym's site — a cross-tenant leak created by
    /// a legitimate caller with a legitimate permission, which no amount of endpoint gating catches.
    /// </summary>
    [Fact]
    public async Task Hiring_into_another_tenants_branch_is_refused()
    {
        var ourGym = await SeedGymAsync();
        var otherGym = await SeedGymAsync();

        SignInAs(ourGym, ourGym.OwnerId);

        await Should.ThrowAsync<ValidationException>(() => SendAsync(new CreateStaffCommand(
            "sneaky@titan.example.com", "Cross", "Tenant", null, RoleNames.Receptionist, [otherGym.DowntownBranchId])));
    }

    /// <summary>
    /// Editing replaces the role and the branch set outright rather than adding to them, because
    /// "moved from Uptown to Downtown" and "given Downtown as well" are different facts and only the
    /// first one can also take access away. A merge-shaped implementation passes every add-a-branch
    /// test and quietly makes branch access permanent.
    /// </summary>
    [Fact]
    public async Task Editing_a_staff_member_replaces_their_role_and_branch_access()
    {
        var gym = await SeedGymAsync();
        SignInAs(gym, gym.OwnerId);

        var created = await SendAsync(new CreateStaffCommand(
            "mover@titan.example.com", "Mover", "Shaker", null, RoleNames.Receptionist,
            [gym.DowntownBranchId, gym.UptownBranchId]));

        await SendAsync(new UpdateStaffCommand(
            created.Id, "Mover", "Shaker-Jones", "+1-555-0199", RoleNames.Accountant, [gym.UptownBranchId]));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var user = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == created.Id);
        user.LastName.ShouldBe("Shaker-Jones");
        user.Phone.ShouldBe("+1-555-0199");
        // Email is not on the update command at all — it is the username, and changing it would
        // invalidate credentials the person already holds.
        user.Email.ShouldBe("mover@titan.example.com");

        var roleIds = await db.UserRoles.Where(ur => ur.UserId == created.Id).Select(ur => ur.RoleId).ToListAsync();
        roleIds.ShouldHaveSingleItem();
        roleIds[0].ShouldBe(gym.RoleIds[RoleNames.Accountant]);

        var branchIds = await db.UserBranchAccesses.Where(uba => uba.UserId == created.Id).Select(uba => uba.BranchId).ToListAsync();
        branchIds.ShouldHaveSingleItem();
        branchIds[0].ShouldBe(gym.UptownBranchId);
    }

    /// <summary>
    /// The Member role is refused on a staff account. A Member row — the record carrying dues,
    /// attendance and membership state — is created by member registration, never here, so a "staff
    /// member" holding the Member role would land on a self-service portal pointed at a member record
    /// that does not exist, and would not appear on the staff list that could correct it.
    /// </summary>
    [Fact]
    public async Task Hiring_someone_into_the_Member_role_is_refused()
    {
        var gym = await SeedGymAsync();
        SignInAs(gym, gym.OwnerId);

        var ex = await Should.ThrowAsync<ValidationException>(() => SendAsync(new CreateStaffCommand(
            "not.a.member@titan.example.com", "Not", "Staff", null, RoleNames.Member, [gym.DowntownBranchId])));

        ex.Message.ShouldContain("Member role");
    }

    /// <summary>
    /// Resetting a password must also end the sessions that password opened — otherwise the reset
    /// protects nothing.
    ///
    /// A reset happens because the old credential is suspected of being in the wrong hands. Whoever
    /// holds a live refresh token can rotate it into fresh access tokens for another week without
    /// ever re-entering a password, so changing the hash alone locks out the legitimate employee and
    /// nobody else. Revoking on reset is the half that does the work.
    /// </summary>
    [Fact]
    public async Task Resetting_a_password_revokes_the_sessions_the_old_one_opened()
    {
        var gym = await SeedGymAsync();
        SignInAs(gym, gym.OwnerId);

        var created = await SendAsync(new CreateStaffCommand(
            "reset.me@titan.example.com", "Reset", "Me", null, RoleNames.Receptionist, [gym.DowntownBranchId]));

        using (var seedScope = CreateScope())
        {
            var seedDb = seedScope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            seedDb.RefreshTokens.Add(new RefreshToken
            {
                UserId = created.Id,
                TokenHash = "live-session-token-hash",
                CreatedAt = DateTimeProvider.UtcNow,
                ExpiresAt = DateTimeProvider.UtcNow.AddDays(7)
            });
            await seedDb.SaveChangesAsync();
        }

        var result = await SendAsync(new ResetStaffPasswordCommand(created.Id));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var user = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == created.Id);
        passwordHasher.Verify(result.TemporaryPassword, user.PasswordHash).ShouldBeTrue();
        passwordHasher.Verify(created.TemporaryPassword, user.PasswordHash).ShouldBeFalse();

        var tokens = await db.RefreshTokens.IgnoreQueryFilters().Where(t => t.UserId == created.Id).ToListAsync();
        tokens.ShouldAllBe(t => t.RevokedAt != null);
    }

    /// <summary>
    /// Reactivation is the way back from a deactivation, and it must not quietly reset the password —
    /// somebody returning from three months' leave signs in with what they had.
    /// </summary>
    [Fact]
    public async Task Reactivating_restores_the_login_without_changing_the_password()
    {
        var gym = await SeedGymAsync();
        SignInAs(gym, gym.OwnerId);

        var created = await SendAsync(new CreateStaffCommand(
            "returner@titan.example.com", "Re", "Turner", null, RoleNames.Trainer, [gym.DowntownBranchId]));

        await SendAsync(new DeactivateStaffCommand(created.Id));
        await SendAsync(new ReactivateStaffCommand(created.Id));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var user = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == created.Id);
        user.IsActive.ShouldBeTrue();
        passwordHasher.Verify(created.TemporaryPassword, user.PasswordHash).ShouldBeTrue();
    }

    private sealed record SeededGym(
        Guid TenantId, Guid DowntownBranchId, Guid UptownBranchId, Guid OwnerId, Dictionary<string, Guid> RoleIds);

    private void SignInAs(SeededGym gym, Guid userId)
    {
        CurrentUser.TenantId = gym.TenantId;
        CurrentUser.UserId = userId;
        CurrentUser.IsAuthenticated = true;
    }

    /// <summary>
    /// Builds one gym the way the real seeder does: every role, the settings.manage_staff permission
    /// granted to Owner and Manager only, two branches, and an Owner who is the sole account able to
    /// hire. The last-manager rule is meaningless against a fixture that skips the RolePermission rows.
    /// </summary>
    private async Task<SeededGym> SeedGymAsync()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Gym-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N"), IsActive = true };
        db.Tenants.Add(tenant);

        var downtown = new Branch { TenantId = tenant.Id, Name = "Downtown", City = "Metropolis", Country = "United States", IsActive = true };
        var uptown = new Branch { TenantId = tenant.Id, Name = "Uptown", City = "Metropolis", Country = "United States", IsActive = true };
        db.Branches.AddRange(downtown, uptown);

        var roles = RoleNames.All.ToDictionary(
            name => name, name => new Role { TenantId = tenant.Id, Name = name, IsSystemRole = true });
        db.Roles.AddRange(roles.Values);

        // The catalogue is global (not tenant-scoped), so the row is created once and reused by every
        // gym this fixture builds.
        var manageStaff = await db.Permissions.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Code == PermissionCodes.Settings.ManageStaff);

        if (manageStaff is null)
        {
            manageStaff = new Permission
            {
                Code = PermissionCodes.Settings.ManageStaff,
                Module = "settings",
                Description = "Create, edit, and deactivate staff accounts"
            };
            db.Permissions.Add(manageStaff);
        }

        foreach (var roleName in new[] { RoleNames.Owner, RoleNames.Manager })
        {
            db.RolePermissions.Add(new RolePermission { RoleId = roles[roleName].Id, PermissionId = manageStaff.Id });
        }

        var owner = new User
        {
            TenantId = tenant.Id,
            Email = $"owner-{Guid.NewGuid():N}@titan.example.com",
            PasswordHash = "seeded-hash",
            FirstName = "Owner",
            LastName = "Account",
            IsActive = true
        };
        db.Users.Add(owner);
        db.UserRoles.Add(new UserRole { UserId = owner.Id, RoleId = roles[RoleNames.Owner].Id });
        db.UserBranchAccesses.Add(new UserBranchAccess { UserId = owner.Id, BranchId = downtown.Id });

        await db.SaveChangesAsync();

        return new SeededGym(
            tenant.Id, downtown.Id, uptown.Id, owner.Id, roles.ToDictionary(r => r.Key, r => r.Value.Id));
    }

    private async Task<Guid> SeedUserAsync(SeededGym gym, string email, string roleName)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var user = new User
        {
            TenantId = gym.TenantId,
            Email = email,
            PasswordHash = "seeded-hash",
            FirstName = roleName,
            LastName = "Person",
            IsActive = true
        };

        db.Users.Add(user);
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = gym.RoleIds[roleName] });
        db.UserBranchAccesses.Add(new UserBranchAccess { UserId = user.Id, BranchId = gym.DowntownBranchId });

        await db.SaveChangesAsync();
        return user.Id;
    }
}
