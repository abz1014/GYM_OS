using GymOS.Application.Modules.Experience.Commands;
using GymOS.Application.Modules.Experience.Queries;
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
/// Slice 5: logging a recovery day rewards recovery consistency once per day (via RecoveryLoggedEvent),
/// and the recovery query classifies the member's status from their own recent training load.
/// </summary>
public class RecoveryEngineTests : ApplicationTestBase
{
    [Fact]
    public async Task Logging_recovery_awards_recovery_xp_once_per_day()
    {
        var ctx = await SeedAsync();
        SetAuthenticatedAs(ctx.TenantId, ctx.UserId);

        await SendAsync(new LogMyRecoveryCommand(RecoveryKind.RestDay, "Full rest day"));
        await SendAsync(new LogMyRecoveryCommand(RecoveryKind.Mobility, "Second log, same day")); // same day

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var recoveryXp = await db.XpTransactions
            .Where(t => t.MemberId == ctx.MemberId && t.Reason == XpReason.RecoveryLogged)
            .ToListAsync();
        recoveryXp.ShouldHaveSingleItem(); // both logs are the same day -> one award
        recoveryXp[0].Amount.ShouldBe(XpPolicy.AwardFor(XpReason.RecoveryLogged)); // 10

        // And only one recovery log exists — the second same-day call returns the existing one.
        var logs = await db.RecoveryLogs.Where(r => r.MemberId == ctx.MemberId).ToListAsync();
        logs.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Recovery_query_classifies_from_recent_training_load()
    {
        var ctx = await SeedAsync();
        SetAuthenticatedAs(ctx.TenantId, ctx.UserId);
        await SeedThisWeekTrainingAsync(ctx);

        var recovery = await SendAsync(new GetMyRecoveryQuery());

        recovery.Status.ShouldNotBeNullOrWhiteSpace();
        recovery.Reason.ShouldNotBeNullOrWhiteSpace();
        recovery.SessionsLast7Days.ShouldBeGreaterThanOrEqualTo(1);
        recovery.RestDaysLast7Days.ShouldBeGreaterThanOrEqualTo(1);
        recovery.DaysSinceLastWorkout.ShouldNotBeNull();
        recovery.MuscleGroups.ShouldContain(m => m.MuscleGroup == "Chest");
    }

    private void SetAuthenticatedAs(Guid tenantId, Guid userId)
    {
        CurrentUser.TenantId = tenantId;
        CurrentUser.UserId = userId;
        CurrentUser.IsAuthenticated = true;
    }

    private async Task SeedThisWeekTrainingAsync((Guid TenantId, Guid BranchId, Guid MemberId, Guid ExerciseId, Guid UserId) ctx)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var now = DateTimeProvider.UtcNow;

        var log = new WorkoutLog { TenantId = ctx.TenantId, MemberId = ctx.MemberId, LoggedAt = now };
        log.Entries.Add(new WorkoutLogEntry { TenantId = ctx.TenantId, ExerciseId = ctx.ExerciseId, SetsCompleted = 3, RepsCompleted = 8, WeightKg = 60m });
        db.WorkoutLogs.Add(log);

        db.RecoveryLogs.Add(new RecoveryLog
        {
            MemberId = ctx.MemberId,
            LoggedOn = DateOnly.FromDateTime(now.UtcDateTime).AddDays(-2),
            Kind = RecoveryKind.RestDay
        });

        await db.SaveChangesAsync();
    }

    private async Task<(Guid TenantId, Guid BranchId, Guid MemberId, Guid ExerciseId, Guid UserId)> SeedAsync()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);

        var branch = new Branch { TenantId = tenant.Id, Name = "Main", AddressLine = "1 Main St", City = "City", Country = "US" };
        db.Branches.Add(branch);

        var user = new User
        {
            TenantId = tenant.Id,
            Email = $"{Guid.NewGuid():N}@example.com",
            PasswordHash = "unused-in-this-test",
            FirstName = "Member",
            LastName = "User"
        };
        db.Users.Add(user);
        db.UserBranchAccesses.Add(new UserBranchAccess { UserId = user.Id, BranchId = branch.Id });

        var member = new Member
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            UserId = user.Id,
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
        return (tenant.Id, branch.Id, member.Id, exercise.Id, user.Id);
    }
}
