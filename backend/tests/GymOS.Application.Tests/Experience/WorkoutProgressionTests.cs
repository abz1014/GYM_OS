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
/// Drives the workout-derived projections through the real LogWorkoutCommand pipeline: a logged
/// workout raises WorkoutLoggedEvent, which the progression handler consumes to append personal
/// records and refresh exercise mastery. Covers first-session baselines, a heavier session beating
/// them, and idempotency (re-applying the same workout changes nothing).
/// </summary>
public class WorkoutProgressionTests : ApplicationTestBase
{
    [Fact]
    public async Task Logging_a_workout_sets_baseline_records_and_mastery()
    {
        var ctx = await SeedAsync();
        SetAuthenticatedAs(ctx.TenantId, ctx.StaffUserId);

        // 3 sets x 8 reps @ 60kg. maxWeight=60, 1RM=Epley(60,8)=76, volume=3*8*60=1440.
        await SendAsync(new LogWorkoutCommand(ctx.MemberId, null, [new WorkoutLogEntryInput(ctx.ExerciseId, 3, 8, 60m)]));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var prs = await db.PersonalRecords.Where(r => r.MemberId == ctx.MemberId).ToListAsync();
        prs.Count.ShouldBe(3);
        prs.Single(p => p.Type == PersonalRecordType.MaxWeight).Value.ShouldBe(60m);
        prs.Single(p => p.Type == PersonalRecordType.EstimatedOneRepMax).Value.ShouldBe(76m);
        prs.Single(p => p.Type == PersonalRecordType.SessionVolume).Value.ShouldBe(1440m);

        var mastery = await db.ExerciseMasteries.SingleAsync(m => m.MemberId == ctx.MemberId && m.ExerciseId == ctx.ExerciseId);
        mastery.Sessions.ShouldBe(1);
        mastery.TotalVolume.ShouldBe(1440m);
        mastery.BestWeightKg.ShouldBe(60m);
        mastery.BestEstimatedOneRepMax.ShouldBe(76m);
    }

    [Fact]
    public async Task A_heavier_session_beats_the_records_and_accumulates_mastery()
    {
        var ctx = await SeedAsync();
        SetAuthenticatedAs(ctx.TenantId, ctx.StaffUserId);

        await SendAsync(new LogWorkoutCommand(ctx.MemberId, null, [new WorkoutLogEntryInput(ctx.ExerciseId, 3, 8, 60m)]));
        // Heavier: 3x8@65. maxWeight 65>60, 1RM Epley(65,8)=82.33>76, volume 1560>1440 -> all three beaten.
        await SendAsync(new LogWorkoutCommand(ctx.MemberId, null, [new WorkoutLogEntryInput(ctx.ExerciseId, 3, 8, 65m)]));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var prs = await db.PersonalRecords.Where(r => r.MemberId == ctx.MemberId).ToListAsync();
        prs.Count.ShouldBe(6); // 3 baseline + 3 new bests
        prs.Where(p => p.Type == PersonalRecordType.MaxWeight).Max(p => p.Value).ShouldBe(65m);

        var mastery = await db.ExerciseMasteries.SingleAsync(m => m.MemberId == ctx.MemberId && m.ExerciseId == ctx.ExerciseId);
        mastery.Sessions.ShouldBe(2);
        mastery.TotalVolume.ShouldBe(3000m); // 1440 + 1560
        mastery.BestWeightKg.ShouldBe(65m);
    }

    [Fact]
    public async Task Re_applying_the_same_workout_is_idempotent()
    {
        var ctx = await SeedAsync();
        SetAuthenticatedAs(ctx.TenantId, ctx.StaffUserId);

        var workoutLogId = await SendAsync(new LogWorkoutCommand(ctx.MemberId, null, [new WorkoutLogEntryInput(ctx.ExerciseId, 3, 8, 60m)]));

        // Re-run the projection application for the very same workout (simulates a re-published event).
        using (var scope = CreateScope())
        {
            var progression = scope.ServiceProvider.GetRequiredService<IWorkoutProgressionService>();
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            await progression.ApplyWorkoutAsync(ctx.MemberId, workoutLogId, default);
            await db.SaveChangesAsync();
        }

        using var verify = CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<GymOsDbContext>();
        (await verifyDb.PersonalRecords.CountAsync(r => r.MemberId == ctx.MemberId)).ShouldBe(3);
        var mastery = await verifyDb.ExerciseMasteries.SingleAsync(m => m.MemberId == ctx.MemberId);
        mastery.Sessions.ShouldBe(1);
        mastery.TotalVolume.ShouldBe(1440m);
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
