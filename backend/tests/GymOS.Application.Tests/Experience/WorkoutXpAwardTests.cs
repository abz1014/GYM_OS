using GymOS.Application.Modules.Experience.Services;
using GymOS.Application.Modules.Workouts.Commands;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Experience;
using GymOS.Domain.Identity;
using GymOS.Domain.Members;
using GymOS.Domain.Tenancy;
using GymOS.Domain.Workouts;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Experience;

/// <summary>
/// End-to-end proof of the Member Experience Engine's event backbone: sending the real
/// LogWorkoutCommand raises WorkoutLoggedEvent, which GymOsDbContext dispatches after save, which the
/// XP handler consumes to append the ledger and advance the level — all inside the one command, with
/// no change to LogWorkoutCommand's external contract. Also pins award idempotency.
/// </summary>
public class WorkoutXpAwardTests : ApplicationTestBase
{
    [Fact]
    public async Task Logging_workouts_awards_xp_through_the_event_backbone_and_levels_up()
    {
        var ctx = await SeedAsync();
        SetAuthenticatedAs(ctx.TenantId, ctx.StaffUserId);

        await SendAsync(new LogWorkoutCommand(ctx.MemberId, null,
            [new WorkoutLogEntryInput(ctx.ExerciseId, 3, 8, 60m)]));

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

            // Two awards, not one: the workout itself (50) plus the improvement (30), because a
            // first-ever lift on this exercise beats a prior best of 0 and therefore sets records.
            // Both are keyed on the same WorkoutLog and separated by Reason.
            var progression = await db.MemberProgressions.SingleAsync(p => p.MemberId == ctx.MemberId);
            progression.TotalXp.ShouldBe(
                XpPolicy.AwardFor(XpReason.WorkoutCompleted) + XpPolicy.AwardFor(XpReason.ProgressiveImprovement)); // 80
            progression.Level.ShouldBe(1); // 80 is still short of the 100 that opens level 2

            var ledger = await db.XpTransactions.Where(t => t.MemberId == ctx.MemberId).ToListAsync();
            ledger.Count.ShouldBe(2);
            ledger.ShouldAllBe(t => t.SourceType == XpSourceType.WorkoutLog);
            ledger.Select(t => t.Reason).ShouldBe(
                [XpReason.WorkoutCompleted, XpReason.ProgressiveImprovement], ignoreOrder: true);
        }

        // A second, distinct workout is a distinct source, so it awards again — 62kg beats 60kg, so
        // it improves too, and the running total of 160 crosses into level 2 (threshold 100).
        await SendAsync(new LogWorkoutCommand(ctx.MemberId, null,
            [new WorkoutLogEntryInput(ctx.ExerciseId, 3, 8, 62m)]));

        using var verify = CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var after = await verifyDb.MemberProgressions.SingleAsync(p => p.MemberId == ctx.MemberId);
        after.TotalXp.ShouldBe(2 * (XpPolicy.AwardFor(XpReason.WorkoutCompleted)
            + XpPolicy.AwardFor(XpReason.ProgressiveImprovement))); // 160
        after.Level.ShouldBe(2);
        (await verifyDb.XpTransactions.CountAsync(t => t.MemberId == ctx.MemberId)).ShouldBe(4);
    }

    [Fact]
    public async Task Awarding_the_same_source_twice_never_double_credits()
    {
        var ctx = await SeedAsync();
        SetAuthenticatedAs(ctx.TenantId, ctx.StaffUserId);

        var sourceId = Guid.NewGuid();

        // Two award attempts for the same (member, source, reason) — e.g. a re-published event.
        using (var scope = CreateScope())
        {
            var xp = scope.ServiceProvider.GetRequiredService<IMemberXpService>();
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            await xp.AwardAsync(ctx.MemberId, XpReason.WorkoutCompleted, XpSourceType.WorkoutLog, sourceId, default);
            await db.SaveChangesAsync();
        }

        using (var scope = CreateScope())
        {
            var xp = scope.ServiceProvider.GetRequiredService<IMemberXpService>();
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            await xp.AwardAsync(ctx.MemberId, XpReason.WorkoutCompleted, XpSourceType.WorkoutLog, sourceId, default);
            await db.SaveChangesAsync();
        }

        using var verify = CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<GymOsDbContext>();
        (await verifyDb.XpTransactions.CountAsync(t => t.MemberId == ctx.MemberId)).ShouldBe(1);
        (await verifyDb.MemberProgressions.SingleAsync(p => p.MemberId == ctx.MemberId)).TotalXp
            .ShouldBe(XpPolicy.AwardFor(XpReason.WorkoutCompleted));
    }

    private void SetAuthenticatedAs(Guid tenantId, Guid userId)
    {
        CurrentUser.TenantId = tenantId;
        CurrentUser.UserId = userId;
        CurrentUser.IsAuthenticated = true;
    }

    private async Task<(Guid TenantId, Guid MemberId, Guid ExerciseId, Guid StaffUserId)> SeedAsync()
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

        var member = new Member
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12],
            FirstName = "Test",
            LastName = "Member",
            Email = $"{Guid.NewGuid():N}@example.com",
            JoinDate = new DateOnly(2025, 1, 1),
            QrCodeToken = Guid.NewGuid().ToString("N")
        };
        db.Members.Add(member);

        var exercise = new Exercise { TenantId = tenant.Id, Name = "Bench Press", MuscleGroup = "Chest", Equipment = "Barbell" };
        db.Exercises.Add(exercise);

        await db.SaveChangesAsync();
        return (tenant.Id, member.Id, exercise.Id, staffUser.Id);
    }
}
