using GymOS.Application.Common.Exceptions;
using GymOS.Application.Modules.Trainers.Commands;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Identity;
using GymOS.Domain.Tenancy;
using GymOS.Domain.Trainers;
using GymOS.Infrastructure.Persistence;
using GymOS.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Trainers;

/// <summary>
/// A trainer could be hired and then never changed. CreateTrainerCommand shipped without an update
/// counterpart, so a coach's specialties, commission rate and bio were frozen at whatever was typed
/// on the day — a new certification or a renegotiated rate had no path into the product at all.
/// </summary>
public class UpdateTrainerCommandTests : ApplicationTestBase
{
    [Fact]
    public async Task Editing_a_trainer_updates_their_profile()
    {
        var (tenantId, branchId, trainerId, _) = await SeedTrainerAsync();
        CurrentUser.TenantId = tenantId;
        CurrentUser.IsAuthenticated = true;

        await SendAsync(new UpdateTrainerCommand(trainerId, "Powerlifting, Mobility", 17.5m, "Twelve years on the floor.", IsActive: true));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var trainer = await db.Trainers.IgnoreQueryFilters().FirstAsync(t => t.Id == trainerId);
        trainer.Specialties.ShouldBe("Powerlifting, Mobility");
        trainer.CommissionRate.ShouldBe(17.5m);
        trainer.Bio.ShouldBe("Twelve years on the floor.");
        trainer.BranchId.ShouldBe(branchId);
    }

    /// <summary>
    /// Standing a trainer down has to end their login too, because on its own it ends nothing.
    ///
    /// Trainer.IsActive is a coaching-roster flag: it removes them from assignment pickers and
    /// schedules. The User row behind it is a separate record, and if that stays active a coach who
    /// stopped working here last month still signs in and still reads every member's profile, medical
    /// notes and history. Employment ending and access ending are one event; a product that treats
    /// them as two leaves ex-staff logged in indefinitely with nothing on screen to suggest it.
    /// </summary>
    [Fact]
    public async Task Standing_a_trainer_down_also_ends_their_login()
    {
        var (tenantId, _, trainerId, userId) = await SeedTrainerAsync();
        CurrentUser.TenantId = tenantId;
        CurrentUser.IsAuthenticated = true;

        await SendAsync(new UpdateTrainerCommand(trainerId, "Strength", 10m, null, IsActive: false));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        (await db.Trainers.IgnoreQueryFilters().FirstAsync(t => t.Id == trainerId)).IsActive.ShouldBeFalse();
        (await db.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == userId)).IsActive.ShouldBeFalse();
    }

    /// <summary>
    /// Another gym's trainer is not found, not forbidden — the handler loads through the tenant- and
    /// branch-filtered DbSet, so a foreign row cannot be edited by guessing its id.
    /// </summary>
    [Fact]
    public async Task Editing_a_trainer_from_another_tenant_is_a_not_found()
    {
        var (tenantId, _, _, _) = await SeedTrainerAsync();
        var (_, _, foreignTrainerId, _) = await SeedTrainerAsync();

        CurrentUser.TenantId = tenantId;
        CurrentUser.IsAuthenticated = true;

        await Should.ThrowAsync<NotFoundException>(
            () => SendAsync(new UpdateTrainerCommand(foreignTrainerId, "Anything", 5m, null, IsActive: false)));
    }

    private async Task<(Guid TenantId, Guid BranchId, Guid TrainerId, Guid UserId)> SeedTrainerAsync()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Gym-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N"), IsActive = true };
        db.Tenants.Add(tenant);

        var branch = new Branch { TenantId = tenant.Id, Name = "Downtown", City = "Metropolis", Country = "United States", IsActive = true };
        db.Branches.Add(branch);

        var role = new Role { TenantId = tenant.Id, Name = RoleNames.Trainer, IsSystemRole = true };
        db.Roles.Add(role);

        var user = new User
        {
            TenantId = tenant.Id,
            Email = $"coach-{Guid.NewGuid():N}@titan.example.com",
            PasswordHash = "seeded-hash",
            FirstName = "Coach",
            LastName = "Person",
            IsActive = true
        };
        db.Users.Add(user);
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        db.UserBranchAccesses.Add(new UserBranchAccess { UserId = user.Id, BranchId = branch.Id });

        var trainer = new Trainer
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            UserId = user.Id,
            Specialties = "Strength",
            CommissionRate = 10m,
            IsActive = true
        };
        db.Trainers.Add(trainer);

        await db.SaveChangesAsync();
        return (tenant.Id, branch.Id, trainer.Id, user.Id);
    }
}
