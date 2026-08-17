using FluentValidation;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Modules.Trainers.Commands;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Identity;
using GymOS.Domain.Tenancy;
using GymOS.Infrastructure.Persistence;
using GymOS.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Trainers;

/// <summary>
/// Hiring a trainer creates their login in the same request — Trainer.UserId is non-nullable, so a
/// Trainer row can never exist without one, unlike the member case where the login was simply never
/// created at all. What was actually missing here was narrower and easy to miss: the email-uniqueness
/// check that guards it was scoped to the caller's own tenant, so a second gym could hire a trainer on
/// an email already in use elsewhere and get an account that collides with the first at sign-in
/// instead of a rejected request.
/// </summary>
public class CreateTrainerCommandTests : ApplicationTestBase
{
    [Fact]
    public async Task Hiring_a_trainer_returns_a_login_that_works_and_grants_the_Trainer_role_and_branch_access()
    {
        var (tenantId, branchId, callerId) = await SeedGymAsync();
        CurrentUser.TenantId = tenantId;
        CurrentUser.UserId = callerId;
        CurrentUser.IsAuthenticated = true;

        var result = await SendAsync(new CreateTrainerCommand(
            "New", "Coach", "new.coach@titan.example.com", branchId, "Strength, Mobility", 15m, "Ten years coaching."));

        result.TemporaryPassword.ShouldNotBeNullOrWhiteSpace();

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var trainer = await db.Trainers.IgnoreQueryFilters().SingleAsync(t => t.Id == result.TrainerId);
        trainer.Specialties.ShouldBe("Strength, Mobility");
        trainer.CommissionRate.ShouldBe(15m);
        trainer.Bio.ShouldBe("Ten years coaching.");
        trainer.BranchId.ShouldBe(branchId);

        var user = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == trainer.UserId);
        user.Email.ShouldBe("new.coach@titan.example.com");
        user.IsActive.ShouldBeTrue();
        passwordHasher.Verify(result.TemporaryPassword, user.PasswordHash).ShouldBeTrue();

        var roleIds = await db.UserRoles.Where(ur => ur.UserId == user.Id).Select(ur => ur.RoleId).ToListAsync();
        roleIds.ShouldHaveSingleItem();
        roleIds[0].ShouldBe((await db.Roles.IgnoreQueryFilters().FirstAsync(r => r.TenantId == tenantId && r.Name == RoleNames.Trainer)).Id);

        var branchIds = await db.UserBranchAccesses.Where(uba => uba.UserId == user.Id).Select(uba => uba.BranchId).ToListAsync();
        branchIds.ShouldHaveSingleItem();
        branchIds[0].ShouldBe(branchId);
    }

    /// <summary>
    /// LoginCommand resolves an email with no tenant context at sign-in time, so a tenant-scoped
    /// uniqueness check would let two different tenants' accounts share an address and strand the
    /// second one with a login that collides with the first rather than ever reaching its own. Checked
    /// here against a real user in a DIFFERENT tenant — the exact case a tenant-scoped check misses.
    /// </summary>
    [Fact]
    public async Task Email_uniqueness_is_checked_globally_not_per_tenant()
    {
        var takenEmail = $"shared-{Guid.NewGuid():N}@titan.example.com";
        await SeedGymAsync(existingUserEmail: takenEmail);

        var (tenantId, branchId, callerId) = await SeedGymAsync();
        CurrentUser.TenantId = tenantId;
        CurrentUser.UserId = callerId;
        CurrentUser.IsAuthenticated = true;

        await Should.ThrowAsync<ValidationException>(() => SendAsync(new CreateTrainerCommand(
            "Dup", "Licate", takenEmail, branchId, "Strength", 10m, null)));
    }

    private async Task<(Guid TenantId, Guid BranchId, Guid CallerId)> SeedGymAsync(string? existingUserEmail = null)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Gym-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N"), IsActive = true };
        db.Tenants.Add(tenant);

        var branch = new Branch { TenantId = tenant.Id, Name = "Downtown", City = "Metropolis", Country = "United States", IsActive = true };
        db.Branches.Add(branch);

        var role = new Role { TenantId = tenant.Id, Name = RoleNames.Trainer, IsSystemRole = true };
        db.Roles.Add(role);

        // The caller who hires — BranchScopeBehavior refuses CreateTrainerCommand unless this
        // account has UserBranchAccess to the branch being hired into.
        var caller = new User
        {
            TenantId = tenant.Id,
            Email = $"owner-{Guid.NewGuid():N}@titan.example.com",
            PasswordHash = "seeded-hash",
            FirstName = "Owner",
            LastName = "Account",
            IsActive = true
        };
        db.Users.Add(caller);
        db.UserBranchAccesses.Add(new UserBranchAccess { UserId = caller.Id, BranchId = branch.Id });

        if (existingUserEmail is not null)
        {
            var user = new User
            {
                TenantId = tenant.Id,
                Email = existingUserEmail,
                PasswordHash = "seeded-hash",
                FirstName = "Existing",
                LastName = "Person",
                IsActive = true
            };
            db.Users.Add(user);
        }

        await db.SaveChangesAsync();
        return (tenant.Id, branch.Id, caller.Id);
    }
}
