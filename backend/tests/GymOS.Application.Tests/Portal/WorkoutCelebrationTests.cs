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

namespace GymOS.Application.Tests.Portal;

/// <summary>
/// What a member is told after logging a session. The whole point is that these numbers are real —
/// the old success toast asserted a flat "+50 XP" whether or not that was what the engine awarded —
/// so each figure is checked against what actually landed in the database.
///
/// Clock fixed to Wednesday 2026-08-05; that week runs Mon 2026-08-03 .. Sun 2026-08-09.
/// </summary>
public class WorkoutCelebrationTests : ApplicationTestBase
{
    private static readonly DateTimeOffset Wednesday = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    public WorkoutCelebrationTests() => DateTimeProvider.UtcNow = Wednesday;

    [Fact]
    public async Task Reported_xp_matches_what_the_ledger_actually_awarded()
    {
        var ctx = await SeedAsync();
        AsMember(ctx);

        var result = await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.ExerciseId, 3, 8, 100m)]));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var ledgerTotal = await db.XpTransactions.Where(t => t.MemberId == ctx.MemberId).SumAsync(t => t.Amount);

        result.XpEarned.ShouldBe(ledgerTotal);
        result.XpEarned.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task A_second_session_reports_only_that_sessions_xp_not_the_running_total()
    {
        var ctx = await SeedAsync();
        AsMember(ctx);

        var first = await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.ExerciseId, 3, 8, 100m)]));
        DateTimeProvider.UtcNow = Wednesday.AddDays(1);
        var second = await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.ExerciseId, 3, 8, 60m)]));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var ledgerTotal = await db.XpTransactions.Where(t => t.MemberId == ctx.MemberId).SumAsync(t => t.Amount);

        second.XpEarned.ShouldBeLessThan(ledgerTotal);
        (first.XpEarned + second.XpEarned).ShouldBe(ledgerTotal);
    }

    [Fact]
    public async Task The_first_session_reports_its_personal_records_and_achievements()
    {
        var ctx = await SeedAsync();
        AsMember(ctx);

        var result = await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.ExerciseId, 3, 8, 100m)]));

        result.NewRecords.ShouldNotBeEmpty();
        result.NewRecords.ShouldAllBe(r => r.ExerciseName == "Deadlift");
        result.NewAchievements.ShouldContain(a => a.Code == "first-workout");
        // The catalog supplies the wording, so the celebration never invents its own copy.
        result.NewAchievements.First(a => a.Code == "first-workout").Name.ShouldBe("First Workout");
    }

    [Fact]
    public async Task Achievements_already_held_are_not_re_announced()
    {
        var ctx = await SeedAsync();
        AsMember(ctx);

        await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.ExerciseId, 3, 8, 100m)]));
        DateTimeProvider.UtcNow = Wednesday.AddDays(1);
        var second = await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.ExerciseId, 3, 8, 50m)]));

        second.NewAchievements.ShouldNotContain(a => a.Code == "first-workout");
    }

    [Fact]
    public async Task Records_belong_to_the_session_that_set_them()
    {
        var ctx = await SeedAsync();
        AsMember(ctx);

        var first = await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.ExerciseId, 3, 8, 100m)]));
        DateTimeProvider.UtcNow = Wednesday.AddDays(1);
        // Strictly lighter than the first session, so it beats nothing.
        var second = await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.ExerciseId, 1, 1, 20m)]));

        first.NewRecords.ShouldNotBeEmpty();
        second.NewRecords.ShouldBeEmpty();
    }

    [Fact]
    public async Task Closing_the_weekly_ring_is_announced_once_not_on_every_session_after_it()
    {
        var ctx = await SeedAsync();
        AsMember(ctx);

        // Default goal is 3; train Mon, Tue, Wed, then a fourth day.
        DateTimeProvider.UtcNow = Wednesday.AddDays(-2);
        var mon = await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.ExerciseId, 3, 8, 60m)]));
        DateTimeProvider.UtcNow = Wednesday.AddDays(-1);
        var tue = await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.ExerciseId, 3, 8, 61m)]));
        DateTimeProvider.UtcNow = Wednesday;
        var wed = await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.ExerciseId, 3, 8, 62m)]));
        DateTimeProvider.UtcNow = Wednesday.AddDays(1);
        var thu = await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.ExerciseId, 3, 8, 63m)]));

        mon.GoalJustMet.ShouldBeFalse();
        tue.GoalJustMet.ShouldBeFalse();
        wed.GoalJustMet.ShouldBeTrue();      // the session that closed it
        thu.GoalJustMet.ShouldBeFalse();     // still met, but not news

        mon.GoalMet.ShouldBeFalse();
        wed.GoalMet.ShouldBeTrue();
        thu.GoalMet.ShouldBeTrue();

        mon.SessionsThisWeek.ShouldBe(1);
        wed.SessionsThisWeek.ShouldBe(3);
        thu.SessionsThisWeek.ShouldBe(4);
    }

    [Fact]
    public async Task The_ring_respects_a_members_own_goal()
    {
        var ctx = await SeedAsync();
        AsMember(ctx);
        await SendAsync(new SetMyWeeklyGoalCommand(1));

        var result = await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.ExerciseId, 3, 8, 60m)]));

        result.WeeklySessionGoal.ShouldBe(1);
        result.GoalJustMet.ShouldBeTrue();
    }

    [Fact]
    public async Task A_bodyweight_session_still_counts_and_still_celebrates()
    {
        var ctx = await SeedAsync();
        AsMember(ctx);

        var result = await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.ExerciseId, 3, 12, null)]));

        result.SessionsThisWeek.ShouldBe(1);
        result.XpEarned.ShouldBeGreaterThan(0);
        result.WorkoutStreakWeeks.ShouldBe(1);
    }

    [Fact]
    public async Task Level_up_is_flagged_only_on_the_session_that_crossed_it()
    {
        var ctx = await SeedAsync();
        AsMember(ctx);

        var results = new List<Application.Modules.Portal.Dtos.MyWorkoutResultDto>();
        for (var day = 0; day < 8; day++)
        {
            DateTimeProvider.UtcNow = Wednesday.AddDays(day);
            results.Add(await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.ExerciseId, 3, 8, 40m + day)])));
        }

        // Whatever the curve, a level-up is reported exactly when the level actually changed.
        var levels = results.Select(r => r.Level).ToList();
        for (var i = 1; i < results.Count; i++)
        {
            results[i].LeveledUp.ShouldBe(levels[i] > levels[i - 1]);
        }
    }

    [Fact]
    public async Task Joined_challenges_report_progress_and_flag_the_session_that_finished_one()
    {
        var ctx = await SeedAsync();
        AsMember(ctx);
        await JoinChallengeAsync(ctx, targetWorkoutCount: 2);

        var first = await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.ExerciseId, 3, 8, 60m)]));
        DateTimeProvider.UtcNow = Wednesday.AddDays(1);
        var second = await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.ExerciseId, 3, 8, 61m)]));

        first.ChallengeProgress.ShouldHaveSingleItem();
        first.ChallengeProgress[0].WorkoutsLogged.ShouldBe(1);
        first.ChallengeProgress[0].TargetWorkoutCount.ShouldBe(2);
        first.ChallengeProgress[0].JustCompleted.ShouldBeFalse();

        second.ChallengeProgress[0].WorkoutsLogged.ShouldBe(2);
        second.ChallengeProgress[0].JustCompleted.ShouldBeTrue();
    }

    [Fact]
    public async Task A_challenge_the_member_has_not_joined_is_not_reported()
    {
        var ctx = await SeedAsync();
        AsMember(ctx);
        await AddChallengeAsync(ctx, targetWorkoutCount: 2);   // exists, but not joined

        var result = await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.ExerciseId, 3, 8, 60m)]));

        result.ChallengeProgress.ShouldBeEmpty();
    }

    [Fact]
    public async Task Another_members_activity_never_shows_up_in_my_celebration()
    {
        var mine = await SeedAsync();
        var theirs = await SeedAsync();

        AsMember(theirs);
        await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(theirs.ExerciseId, 5, 10, 200m)]));

        AsMember(mine);
        var result = await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(mine.ExerciseId, 3, 8, 60m)]));

        result.SessionsThisWeek.ShouldBe(1);
        result.NewRecords.ShouldAllBe(r => r.ExerciseName == "Deadlift");
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var myLedger = await db.XpTransactions.Where(t => t.MemberId == mine.MemberId).SumAsync(t => t.Amount);
        result.XpEarned.ShouldBe(myLedger);
    }

    private void AsMember(SeedContext ctx)
    {
        CurrentUser.TenantId = ctx.TenantId;
        CurrentUser.UserId = ctx.MemberUserId;
        CurrentUser.IsAuthenticated = true;
    }

    private async Task<Guid> AddChallengeAsync(SeedContext ctx, int targetWorkoutCount)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var challenge = new CommunityChallenge
        {
            TenantId = ctx.TenantId, BranchId = null, Name = "August Push",
            StartDate = new DateOnly(2026, 8, 1), EndDate = new DateOnly(2026, 8, 31),
            TargetWorkoutCount = targetWorkoutCount
        };
        db.CommunityChallenges.Add(challenge);
        await db.SaveChangesAsync();
        return challenge.Id;
    }

    private async Task JoinChallengeAsync(SeedContext ctx, int targetWorkoutCount)
    {
        var challengeId = await AddChallengeAsync(ctx, targetWorkoutCount);
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        db.ChallengeParticipants.Add(new ChallengeParticipant
        {
            ChallengeId = challengeId, MemberId = ctx.MemberId, JoinedAt = DateTimeProvider.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private record SeedContext(Guid TenantId, Guid BranchId, Guid MemberId, Guid ExerciseId, Guid MemberUserId);

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

        var exercise = new Exercise { TenantId = tenant.Id, Name = "Deadlift", MuscleGroup = "Back", Equipment = "Barbell" };
        db.Exercises.Add(exercise);

        await db.SaveChangesAsync();
        return new SeedContext(tenant.Id, branch.Id, member.Id, exercise.Id, user.Id);
    }
}
