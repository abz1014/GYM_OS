using GymOS.Application.Modules.Workouts.Commands;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Attendance;
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
/// Proves the two-pass dispatch end to end: logging a workout awards XP (pass 1), which makes
/// MemberProgression raise MemberProgressionChanged, whose handler evaluates achievements against the
/// now-committed stats (pass 2) and unlocks the earned ones — once each.
/// </summary>
public class AchievementUnlockTests : ApplicationTestBase
{
    [Fact]
    public async Task Logging_a_workout_unlocks_the_first_steps_from_committed_stats()
    {
        var ctx = await SeedAsync();
        SetAuthenticatedAs(ctx.TenantId, ctx.StaffUserId);
        await AddAttendanceAsync(ctx); // gives a committed visit for the "first-visit" stat

        await SendAsync(new LogWorkoutCommand(ctx.MemberId, null, [new WorkoutLogEntryInput(ctx.ExerciseId, 3, 8, 60m)]));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var codes = await db.MemberAchievements.Where(a => a.MemberId == ctx.MemberId).Select(a => a.Code).ToListAsync();

        codes.ShouldContain("first-workout");
        codes.ShouldContain("first-visit"); // read from the committed AttendanceRecord in the 2nd pass
        codes.ShouldContain("first-pr");    // the weighted workout set a PR in pass 1
        codes.ShouldNotContain("level-3");  // only 50 XP so far
    }

    [Fact]
    public async Task Crossing_the_level_threshold_unlocks_the_level_badge_and_never_duplicates()
    {
        var ctx = await SeedAsync();
        SetAuthenticatedAs(ctx.TenantId, ctx.StaffUserId);

        // 6 workouts x 50 XP = 300 XP -> level 3 (cumulative threshold for level 3 is 300).
        for (var i = 0; i < 6; i++)
        {
            await SendAsync(new LogWorkoutCommand(ctx.MemberId, null, [new WorkoutLogEntryInput(ctx.ExerciseId, 3, 8, 60m + i)]));
        }

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var codes = await db.MemberAchievements.Where(a => a.MemberId == ctx.MemberId).Select(a => a.Code).ToListAsync();

        codes.ShouldContain("level-3");
        // Idempotency: repeated evaluation across six workouts unlocked each badge exactly once.
        codes.Count(c => c == "first-workout").ShouldBe(1);
        codes.Count(c => c == "level-3").ShouldBe(1);
    }

    private void SetAuthenticatedAs(Guid tenantId, Guid userId)
    {
        CurrentUser.TenantId = tenantId;
        CurrentUser.UserId = userId;
        CurrentUser.IsAuthenticated = true;
    }

    private async Task AddAttendanceAsync((Guid TenantId, Guid BranchId, Guid MemberId, Guid ExerciseId, Guid StaffUserId) ctx)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        db.AttendanceRecords.Add(new AttendanceRecord
        {
            TenantId = ctx.TenantId,
            BranchId = ctx.BranchId,
            MemberId = ctx.MemberId,
            CheckInAt = DateTimeOffset.UtcNow.AddDays(-1),
            Method = AttendanceMethod.Manual
        });
        await db.SaveChangesAsync();
    }

    private async Task<(Guid TenantId, Guid BranchId, Guid MemberId, Guid ExerciseId, Guid StaffUserId)> SeedAsync()
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
        return (tenant.Id, branch.Id, member.Id, exercise.Id, staffUser.Id);
    }
}
