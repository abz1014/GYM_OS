using GymOS.Application.Modules.Members.Queries;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Identity;
using GymOS.Domain.Members;
using GymOS.Domain.Tenancy;
using GymOS.Domain.Trainers;
using GymOS.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Members;

/// <summary>
/// The "Coached by" line on the member workspace. The front desk answers "who trains this member?"
/// off the member detail itself, so the DTO resolves the ACTIVE pairing — an ended one must come
/// back null, because "your coach" and "your old coach" are different answers to the question a
/// member at the counter is asking.
/// </summary>
public class MemberAssignedTrainerTests : ApplicationTestBase
{
    [Fact]
    public async Task Member_detail_names_the_active_trainer()
    {
        var seeded = await SeedGymWithTrainerAsync();
        SetAuthenticatedAs(seeded.TenantId, seeded.StaffUserId);

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            db.TrainerAssignments.Add(new TrainerAssignment
            {
                // Tenant-scoped now; the filter fails closed if this is left at Guid.Empty.
                TenantId = seeded.TenantId,
                TrainerId = seeded.TrainerId,
                MemberId = seeded.MemberId,
                StartDate = new DateOnly(2026, 1, 1),
                IsActive = true
            });
            await db.SaveChangesAsync();
        }

        var detail = await SendAsync(new GetMemberByIdQuery(seeded.MemberId));

        detail.AssignedTrainerId.ShouldBe(seeded.TrainerId);
        detail.AssignedTrainerName.ShouldBe("Maureen Wolff");
    }

    [Fact]
    public async Task An_ended_pairing_is_not_an_assigned_trainer()
    {
        var seeded = await SeedGymWithTrainerAsync();
        SetAuthenticatedAs(seeded.TenantId, seeded.StaffUserId);

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            db.TrainerAssignments.Add(new TrainerAssignment
            {
                // Tenant-scoped now; the filter fails closed if this is left at Guid.Empty.
                TenantId = seeded.TenantId,
                TrainerId = seeded.TrainerId,
                MemberId = seeded.MemberId,
                StartDate = new DateOnly(2025, 6, 1),
                EndDate = new DateOnly(2025, 12, 1),
                IsActive = false
            });
            await db.SaveChangesAsync();
        }

        var detail = await SendAsync(new GetMemberByIdQuery(seeded.MemberId));

        detail.AssignedTrainerId.ShouldBeNull();
        detail.AssignedTrainerName.ShouldBeNull();
    }

    [Fact]
    public async Task A_member_with_no_pairing_resolves_null_rather_than_failing()
    {
        var seeded = await SeedGymWithTrainerAsync();
        SetAuthenticatedAs(seeded.TenantId, seeded.StaffUserId);

        var detail = await SendAsync(new GetMemberByIdQuery(seeded.MemberId));

        detail.AssignedTrainerName.ShouldBeNull();
    }

    private void SetAuthenticatedAs(Guid tenantId, Guid userId)
    {
        CurrentUser.TenantId = tenantId;
        CurrentUser.UserId = userId;
        CurrentUser.IsAuthenticated = true;
    }

    private async Task<(Guid TenantId, Guid StaffUserId, Guid MemberId, Guid TrainerId)> SeedGymWithTrainerAsync()
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

        // The trainer's display name lives on their linked User, not the Trainer row — the same
        // resolution the query under test performs.
        var trainerUser = new User
        {
            TenantId = tenant.Id,
            Email = $"{Guid.NewGuid():N}@example.com",
            PasswordHash = "unused-in-this-test",
            FirstName = "Maureen",
            LastName = "Wolff"
        };
        db.Users.Add(trainerUser);

        var trainer = new Trainer { TenantId = tenant.Id, BranchId = branch.Id, UserId = trainerUser.Id };
        db.Trainers.Add(trainer);

        var member = new Member
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12],
            FirstName = "Front",
            LastName = "Desk",
            Email = $"{Guid.NewGuid():N}@example.com",
            JoinDate = DateOnly.FromDateTime(DateTime.UtcNow),
            QrCodeToken = Guid.NewGuid().ToString("N")
        };
        db.Members.Add(member);

        await db.SaveChangesAsync();
        return (tenant.Id, staffUser.Id, member.Id, trainer.Id);
    }
}
