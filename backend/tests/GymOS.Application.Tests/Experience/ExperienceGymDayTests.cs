using GymOS.Application.Modules.Experience.Commands;
using GymOS.Application.Modules.Experience.Queries;
using GymOS.Application.Modules.Portal.Commands;
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
/// The Experience module counts days too, and it went on counting them in UTC after the rest of the
/// member surface moved to the gym's clock. That mattered more here than anywhere else: recovery is
/// the first thing the home screen says, so the screen was reading its headline off one calendar and
/// everything beneath it off another.
///
/// Every case below is an evening in New York — the hours most people actually train, and the only
/// ones where a UTC day and a local day disagree.
/// </summary>
public class ExperienceGymDayTests : ApplicationTestBase
{
    [Fact]
    public async Task An_evening_session_is_not_still_todays_session_tomorrow_morning()
    {
        // 8:30pm Wednesday in New York is already Thursday in UTC. Read on Thursday morning, a UTC
        // day count calls that session "today" and tells the member they are still recovering from a
        // workout they finished the previous evening.
        var ctx = await SeedAsync("America/New_York");
        SetAuthenticatedAs(ctx.TenantId, ctx.UserId);

        DateTimeProvider.UtcNow = new DateTimeOffset(2026, 8, 5, 20, 30, 0, TimeSpan.FromHours(-4));
        await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.ExerciseId, 3, 10, 40m)]));

        DateTimeProvider.UtcNow = new DateTimeOffset(2026, 8, 6, 9, 0, 0, TimeSpan.FromHours(-4));
        var recovery = await SendAsync(new GetMyRecoveryQuery());

        recovery.DaysSinceLastWorkout.ShouldBe(1);
        recovery.MuscleGroups.ShouldContain(m => m.MuscleGroup == "Chest" && m.DaysSinceLastTrained == 1);
    }

    [Fact]
    public async Task A_rest_day_logged_in_the_evening_belongs_to_that_evening()
    {
        // Written on the UTC date, a 9pm rest day is stored as tomorrow — missing from the window
        // that asks "did you rest today", and present in the one that asks about a day not yet lived.
        var ctx = await SeedAsync("America/New_York");
        SetAuthenticatedAs(ctx.TenantId, ctx.UserId);

        DateTimeProvider.UtcNow = new DateTimeOffset(2026, 8, 5, 21, 0, 0, TimeSpan.FromHours(-4));
        await SendAsync(new LogMyRecoveryCommand(RecoveryKind.RestDay, null));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var log = await db.RecoveryLogs.SingleAsync(r => r.MemberId == ctx.MemberId);

        log.LoggedOn.ShouldBe(new DateOnly(2026, 8, 5));
    }

    [Fact]
    public async Task A_sunday_evening_session_keeps_the_streak_it_belongs_to()
    {
        // The same defect MyTodayTests pins for the home screen, on the standalone streaks endpoint.
        // Two weeks trained back to back: the only session of the first is Sunday 8:30pm in New York,
        // which is already Monday in UTC. Counted that way it deserts the week it finished and piles
        // into the next, leaving the first week empty — so a member who trained two weeks running is
        // told they are on week one.
        var ctx = await SeedAsync("America/New_York");
        SetAuthenticatedAs(ctx.TenantId, ctx.UserId);

        // Week of Mon 3 Aug – Sun 9 Aug.
        DateTimeProvider.UtcNow = new DateTimeOffset(2026, 8, 9, 20, 30, 0, TimeSpan.FromHours(-4));
        await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.ExerciseId, 3, 10, 40m)]));

        // Week of Mon 10 Aug – Sun 16 Aug, at midday where the two calendars agree.
        DateTimeProvider.UtcNow = new DateTimeOffset(2026, 8, 12, 12, 0, 0, TimeSpan.FromHours(-4));
        await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.ExerciseId, 3, 10, 40m)]));

        (await SendAsync(new GetMyStreaksQuery())).WorkoutWeeks.ShouldBe(2);
    }

    [Fact]
    public async Task A_gym_with_an_unreadable_timezone_still_gets_its_recovery_read()
    {
        // Same contract as everywhere else: a fat-fingered setting degrades to UTC days, it does not
        // take the member's home screen down with it.
        var ctx = await SeedAsync("Not/AZone");
        SetAuthenticatedAs(ctx.TenantId, ctx.UserId);

        DateTimeProvider.UtcNow = new DateTimeOffset(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);
        await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.ExerciseId, 3, 10, 40m)]));

        (await SendAsync(new GetMyRecoveryQuery())).DaysSinceLastWorkout.ShouldBe(0);
    }

    private void SetAuthenticatedAs(Guid tenantId, Guid userId)
    {
        CurrentUser.TenantId = tenantId;
        CurrentUser.UserId = userId;
        CurrentUser.IsAuthenticated = true;
    }

    private async Task<(Guid TenantId, Guid MemberId, Guid ExerciseId, Guid UserId)> SeedAsync(string timeZoneId)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);

        var branch = new Branch
        {
            TenantId = tenant.Id, Name = "Main", AddressLine = "1 Main St", City = "City", Country = "US",
            TimeZone = timeZoneId
        };
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
        return (tenant.Id, member.Id, exercise.Id, user.Id);
    }
}
