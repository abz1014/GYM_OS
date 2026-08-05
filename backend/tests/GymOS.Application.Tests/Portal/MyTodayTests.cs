using GymOS.Application.Modules.Experience.Queries;
using GymOS.Application.Modules.Portal.Commands;
using GymOS.Application.Modules.Portal.Queries;
using GymOS.Application.Modules.Workouts.Commands;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Classes;
using GymOS.Domain.Identity;
using GymOS.Domain.Members;
using GymOS.Domain.Tenancy;
using GymOS.Domain.Workouts;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using ValidationException = FluentValidation.ValidationException;

namespace GymOS.Application.Tests.Portal;

/// <summary>
/// The member home screen aggregate and the weekly goal behind it.
///
/// This logic used to live in the browser, where it was untestable and — as the bodyweight test
/// below pins — wrong. These tests exist so that "what counts as a session this week" has exactly
/// one answer, and it's checked.
///
/// The clock is fixed to Wednesday 2026-08-05; that week runs Mon 2026-08-03 .. Sun 2026-08-09.
/// </summary>
public class MyTodayTests : ApplicationTestBase
{
    private static readonly DateTimeOffset Wednesday = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    public MyTodayTests() => DateTimeProvider.UtcNow = Wednesday;

    [Fact]
    public async Task A_bodyweight_session_counts_towards_the_week()
    {
        // The regression that motivated moving this server-side: the browser derived sessions from
        // lifting VOLUME (sets x reps x weight), so a session logged without weight scored zero and
        // vanished from the ring — a member doing pull-ups and cardio never closed it.
        var ctx = await SeedAsync();
        AsMember(ctx);

        await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.ExerciseId, 3, 12, null)]));

        var today = await SendAsync(new GetMyTodayQuery());

        today.SessionsThisWeek.ShouldBe(1);
        today.RemainingSessions.ShouldBe(2);
        today.GoalMet.ShouldBeFalse();
    }

    [Fact]
    public async Task Two_workouts_on_one_day_are_one_session()
    {
        var ctx = await SeedAsync();
        AsMember(ctx);

        await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.ExerciseId, 3, 8, 60m)]));
        await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.ExerciseId, 2, 10, 40m)]));

        (await SendAsync(new GetMyTodayQuery())).SessionsThisWeek.ShouldBe(1);
    }

    [Fact]
    public async Task Sessions_from_last_week_do_not_count_towards_this_week()
    {
        var ctx = await SeedAsync();
        AsMember(ctx);

        DateTimeProvider.UtcNow = Wednesday.AddDays(-3);   // Sunday 2026-08-02 — the previous week
        await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.ExerciseId, 3, 8, 60m)]));

        DateTimeProvider.UtcNow = Wednesday;
        var today = await SendAsync(new GetMyTodayQuery());

        today.SessionsThisWeek.ShouldBe(0);
        today.RemainingSessions.ShouldBe(WeeklyGoalPolicy.DefaultWeeklySessionGoal);
    }

    [Fact]
    public async Task Monday_is_inside_the_current_week()
    {
        var ctx = await SeedAsync();
        AsMember(ctx);

        DateTimeProvider.UtcNow = Wednesday.AddDays(-2);   // Monday 2026-08-03
        await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.ExerciseId, 3, 8, 60m)]));

        DateTimeProvider.UtcNow = Wednesday;
        (await SendAsync(new GetMyTodayQuery())).SessionsThisWeek.ShouldBe(1);
    }

    [Fact]
    public async Task A_member_who_has_never_chosen_a_goal_gets_the_default()
    {
        var ctx = await SeedAsync();
        AsMember(ctx);

        var today = await SendAsync(new GetMyTodayQuery());

        today.WeeklySessionGoal.ShouldBe(WeeklyGoalPolicy.DefaultWeeklySessionGoal);

        // No row is written just to hold a default — "never customised" stays distinguishable.
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        (await db.MemberTrainingPreferences.AnyAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task Setting_a_goal_changes_what_the_ring_closes_at()
    {
        var ctx = await SeedAsync();
        AsMember(ctx);
        await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.ExerciseId, 3, 8, 60m)]));

        await SendAsync(new SetMyWeeklyGoalCommand(1));

        var today = await SendAsync(new GetMyTodayQuery());
        today.WeeklySessionGoal.ShouldBe(1);
        today.SessionsThisWeek.ShouldBe(1);
        today.RemainingSessions.ShouldBe(0);
        today.GoalMet.ShouldBeTrue();
    }

    [Fact]
    public async Task Changing_the_goal_twice_updates_the_one_row_rather_than_adding_another()
    {
        var ctx = await SeedAsync();
        AsMember(ctx);

        await SendAsync(new SetMyWeeklyGoalCommand(4));
        await SendAsync(new SetMyWeeklyGoalCommand(6));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var preference = await db.MemberTrainingPreferences.SingleAsync();
        preference.WeeklySessionGoal.ShouldBe(6);
        preference.MemberId.ShouldBe(ctx.MemberId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(15)]
    public async Task A_goal_outside_the_allowed_range_is_rejected(int goal)
    {
        var ctx = await SeedAsync();
        AsMember(ctx);

        await Should.ThrowAsync<ValidationException>(() => SendAsync(new SetMyWeeklyGoalCommand(goal)));
    }

    [Fact]
    public async Task My_goal_and_my_sessions_are_not_visible_to_another_member()
    {
        var mine = await SeedAsync();
        var theirs = await SeedAsync();

        AsMember(mine);
        await SendAsync(new SetMyWeeklyGoalCommand(7));
        await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(mine.ExerciseId, 3, 8, 60m)]));

        AsMember(theirs);
        var theirToday = await SendAsync(new GetMyTodayQuery());

        theirToday.WeeklySessionGoal.ShouldBe(WeeklyGoalPolicy.DefaultWeeklySessionGoal);
        theirToday.SessionsThisWeek.ShouldBe(0);
    }

    [Fact]
    public async Task The_greeting_name_comes_from_the_member_profile()
    {
        var ctx = await SeedAsync();
        AsMember(ctx);

        (await SendAsync(new GetMyTodayQuery())).FirstName.ShouldBe("Test");
    }

    [Fact]
    public async Task The_streak_matches_what_the_streaks_query_reports()
    {
        // Ring and flame sit side by side on the home screen; they must agree, and they now share
        // both the same rows and the same pure calculator.
        var ctx = await SeedAsync();
        AsMember(ctx);
        await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.ExerciseId, 3, 8, 60m)]));

        var today = await SendAsync(new GetMyTodayQuery());
        var streaks = await SendAsync(new GetMyStreaksQuery());

        today.WorkoutStreakWeeks.ShouldBe(streaks.WorkoutWeeks);
        today.WorkoutStreakWeeks.ShouldBe(1);
    }

    [Fact]
    public async Task A_class_booked_for_today_is_surfaced_and_one_booked_later_is_not()
    {
        var ctx = await SeedAsync();
        AsMember(ctx);
        await BookClassAsync(ctx, Wednesday.AddHours(3));    // later today
        await BookClassAsync(ctx, Wednesday.AddDays(2));     // Friday

        var today = await SendAsync(new GetMyTodayQuery());

        today.NextClassToday.ShouldNotBeNull();
        today.NextClassToday!.StartsAt.ShouldBe(Wednesday.AddHours(3));
    }

    [Fact]
    public async Task No_class_today_leaves_the_slot_empty_rather_than_showing_the_next_one()
    {
        var ctx = await SeedAsync();
        AsMember(ctx);
        await BookClassAsync(ctx, Wednesday.AddDays(2));     // Friday only

        (await SendAsync(new GetMyTodayQuery())).NextClassToday.ShouldBeNull();
    }

    [Fact]
    public async Task A_class_that_already_finished_today_is_not_surfaced()
    {
        var ctx = await SeedAsync();
        AsMember(ctx);
        await BookClassAsync(ctx, Wednesday.AddHours(-3));   // earlier today

        (await SendAsync(new GetMyTodayQuery())).NextClassToday.ShouldBeNull();
    }

    private void AsMember(SeedContext ctx)
    {
        CurrentUser.TenantId = ctx.TenantId;
        CurrentUser.UserId = ctx.MemberUserId;
        CurrentUser.IsAuthenticated = true;
    }

    private async Task BookClassAsync(SeedContext ctx, DateTimeOffset startsAt)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var session = new ClassSession
        {
            TenantId = ctx.TenantId, BranchId = ctx.BranchId, ClassTypeId = ctx.ClassTypeId,
            StartsAt = startsAt, DurationMinutes = 45, Capacity = 20, Location = "Studio A"
        };
        db.ClassSessions.Add(session);
        db.ClassBookings.Add(new ClassBooking
        {
            TenantId = ctx.TenantId, BranchId = ctx.BranchId, ClassSessionId = session.Id,
            MemberId = ctx.MemberId, Status = ClassBookingStatus.Booked, BookedAt = DateTimeProvider.UtcNow
        });

        await db.SaveChangesAsync();
    }

    private record SeedContext(Guid TenantId, Guid BranchId, Guid MemberId, Guid ExerciseId, Guid ClassTypeId, Guid MemberUserId);

    private async Task<SeedContext> SeedAsync()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);

        var branch = new Branch { TenantId = tenant.Id, Name = "Main", AddressLine = "1 Main St", City = "City", Country = "US" };
        db.Branches.Add(branch);

        var user = new User
        {
            TenantId = tenant.Id, Email = $"{Guid.NewGuid():N}@example.com", PasswordHash = "unused-in-this-test",
            FirstName = "Member", LastName = "User"
        };
        db.Users.Add(user);
        db.UserBranchAccesses.Add(new UserBranchAccess { UserId = user.Id, BranchId = branch.Id });

        var member = new Member
        {
            TenantId = tenant.Id, BranchId = branch.Id, UserId = user.Id,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12], FirstName = "Test", LastName = "Member",
            Email = $"{Guid.NewGuid():N}@example.com", JoinDate = new DateOnly(2025, 1, 1),
            QrCodeToken = Guid.NewGuid().ToString("N")
        };
        db.Members.Add(member);

        var exercise = new Exercise { TenantId = tenant.Id, Name = "Pull-up", MuscleGroup = "Back", Equipment = "Bodyweight" };
        db.Exercises.Add(exercise);

        var classType = new ClassType { TenantId = tenant.Id, Name = "Strength Circuit", ColorHex = "#7c3aed" };
        db.ClassTypes.Add(classType);

        await db.SaveChangesAsync();
        return new SeedContext(tenant.Id, branch.Id, member.Id, exercise.Id, classType.Id, user.Id);
    }
}
