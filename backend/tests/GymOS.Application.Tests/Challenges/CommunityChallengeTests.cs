using GymOS.Application.Modules.Challenges.Commands;
using GymOS.Application.Modules.Challenges.Queries;
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
using ValidationException = FluentValidation.ValidationException;

namespace GymOS.Application.Tests.Challenges;

/// <summary>
/// Slice 8: joining/leaving a community challenge, and completing one by actually logging the
/// required workouts through the real event pipeline (LogWorkoutCommand → WorkoutLoggedEvent →
/// EvaluateChallengeProgressOnWorkoutLoggedHandler) — proving completion, the challenge-XP award, and
/// the "first-challenge" achievement all wire together without any dedicated "complete" action.
/// </summary>
public class CommunityChallengeTests : ApplicationTestBase
{
    [Fact]
    public async Task Joining_a_challenge_the_members_history_already_clears_completes_it_immediately()
    {
        // A real gap caught in live verification: logging the qualifying workouts BEFORE joining must
        // still complete the challenge on join, not require one more workout afterward to trigger it.
        var ctx = await SeedAsync();
        SetAuthenticatedAs(ctx.TenantId, ctx.UserId);
        var today = DateTimeProvider.UtcNow;

        await LogWorkoutOnAsync(ctx.MemberId, ctx.ExerciseId, today);
        await LogWorkoutOnAsync(ctx.MemberId, ctx.ExerciseId, today.AddDays(-1));

        var challengeId = await SeedChallengeAsync(ctx.TenantId, targetWorkoutCount: 2,
            startDate: DateOnly.FromDateTime(today.UtcDateTime).AddDays(-3), endDate: DateOnly.FromDateTime(today.UtcDateTime).AddDays(3));

        await SendAsync(new JoinChallengeCommand(challengeId));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var participation = await db.ChallengeParticipants.FirstAsync(p => p.ChallengeId == challengeId && p.MemberId == ctx.MemberId);
        participation.IsCompleted.ShouldBeTrue();

        var challengeXp = await db.XpTransactions.CountAsync(t => t.MemberId == ctx.MemberId && t.Reason == XpReason.ChallengeCompleted);
        challengeXp.ShouldBe(1);
    }

    [Fact]
    public async Task Joining_twice_is_idempotent()
    {
        var ctx = await SeedAsync();
        SetAuthenticatedAs(ctx.TenantId, ctx.UserId);
        var challengeId = await SeedChallengeAsync(ctx.TenantId, targetWorkoutCount: 5);

        await SendAsync(new JoinChallengeCommand(challengeId));
        await SendAsync(new JoinChallengeCommand(challengeId));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var count = await db.ChallengeParticipants.CountAsync(p => p.ChallengeId == challengeId && p.MemberId == ctx.MemberId);
        count.ShouldBe(1);
    }

    [Fact]
    public async Task Leaving_removes_the_participation_and_leaving_again_is_a_no_op()
    {
        var ctx = await SeedAsync();
        SetAuthenticatedAs(ctx.TenantId, ctx.UserId);
        var challengeId = await SeedChallengeAsync(ctx.TenantId, targetWorkoutCount: 5);

        await SendAsync(new JoinChallengeCommand(challengeId));
        await SendAsync(new LeaveChallengeCommand(challengeId));
        await SendAsync(new LeaveChallengeCommand(challengeId)); // no-op, must not throw

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        (await db.ChallengeParticipants.AnyAsync(p => p.ChallengeId == challengeId && p.MemberId == ctx.MemberId))
            .ShouldBeFalse();
    }

    [Fact]
    public async Task Logging_enough_workouts_completes_the_challenge_and_awards_xp_and_achievement_once()
    {
        var ctx = await SeedAsync();
        SetAuthenticatedAs(ctx.TenantId, ctx.UserId);
        var today = DateTimeProvider.UtcNow;
        var challengeId = await SeedChallengeAsync(ctx.TenantId, targetWorkoutCount: 2, startDate: DateOnly.FromDateTime(today.UtcDateTime).AddDays(-3), endDate: DateOnly.FromDateTime(today.UtcDateTime).AddDays(3));

        await SendAsync(new JoinChallengeCommand(challengeId));

        await LogWorkoutOnAsync(ctx.MemberId, ctx.ExerciseId, today);
        await LogWorkoutOnAsync(ctx.MemberId, ctx.ExerciseId, today.AddDays(1)); // 2nd workout -> hits the target

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

            var participation = await db.ChallengeParticipants
                .FirstAsync(p => p.ChallengeId == challengeId && p.MemberId == ctx.MemberId);
            participation.IsCompleted.ShouldBeTrue();
            participation.CompletedAt.ShouldNotBeNull();

            var challengeXp = await db.XpTransactions
                .Where(t => t.MemberId == ctx.MemberId && t.Reason == XpReason.ChallengeCompleted)
                .ToListAsync();
            challengeXp.ShouldHaveSingleItem();
            challengeXp[0].Amount.ShouldBe(XpPolicy.AwardFor(XpReason.ChallengeCompleted)); // 150

            (await db.MemberAchievements.AnyAsync(a => a.MemberId == ctx.MemberId && a.Code == "first-challenge"))
                .ShouldBeTrue();
        }

        // A third workout after completion must not double-award.
        await LogWorkoutOnAsync(ctx.MemberId, ctx.ExerciseId, today.AddDays(2));

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            var challengeXp = await db.XpTransactions
                .CountAsync(t => t.MemberId == ctx.MemberId && t.Reason == XpReason.ChallengeCompleted);
            challengeXp.ShouldBe(1);
        }
    }

    [Fact]
    public async Task A_completed_challenge_cannot_be_left()
    {
        var ctx = await SeedAsync();
        SetAuthenticatedAs(ctx.TenantId, ctx.UserId);
        var today = DateTimeProvider.UtcNow;
        var challengeId = await SeedChallengeAsync(ctx.TenantId, targetWorkoutCount: 1, startDate: DateOnly.FromDateTime(today.UtcDateTime), endDate: DateOnly.FromDateTime(today.UtcDateTime));

        await SendAsync(new JoinChallengeCommand(challengeId));
        await LogWorkoutOnAsync(ctx.MemberId, ctx.ExerciseId, today);

        await Should.ThrowAsync<ValidationException>(() => SendAsync(new LeaveChallengeCommand(challengeId)));
    }

    [Fact]
    public async Task GetMyChallengesQuery_reports_joined_completed_and_progress()
    {
        var ctx = await SeedAsync();
        SetAuthenticatedAs(ctx.TenantId, ctx.UserId);
        var today = DateTimeProvider.UtcNow;
        var challengeId = await SeedChallengeAsync(ctx.TenantId, targetWorkoutCount: 5, startDate: DateOnly.FromDateTime(today.UtcDateTime).AddDays(-3), endDate: DateOnly.FromDateTime(today.UtcDateTime).AddDays(3));

        await SendAsync(new JoinChallengeCommand(challengeId));
        await LogWorkoutOnAsync(ctx.MemberId, ctx.ExerciseId, today);

        var challenges = await SendAsync(new GetMyChallengesQuery());

        var dto = challenges.ShouldHaveSingleItem();
        dto.Joined.ShouldBeTrue();
        dto.IsCompleted.ShouldBeFalse(); // only 1 of 5
        dto.MyWorkoutCount.ShouldBe(1);
    }

    private void SetAuthenticatedAs(Guid tenantId, Guid userId)
    {
        CurrentUser.TenantId = tenantId;
        CurrentUser.UserId = userId;
        CurrentUser.IsAuthenticated = true;
    }

    private async Task LogWorkoutOnAsync(Guid memberId, Guid exerciseId, DateTimeOffset loggedAt)
    {
        DateTimeProvider.UtcNow = loggedAt;
        await SendAsync(new LogWorkoutCommand(memberId, null, [new WorkoutLogEntryInput(exerciseId, 3, 8, 60m)]));
    }

    private async Task<Guid> SeedChallengeAsync(Guid tenantId, int targetWorkoutCount, DateOnly? startDate = null, DateOnly? endDate = null)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var today = DateOnly.FromDateTime(DateTimeProvider.UtcNow.UtcDateTime);

        var challenge = new CommunityChallenge
        {
            TenantId = tenantId,
            BranchId = null,
            Name = "Test Challenge",
            StartDate = startDate ?? today.AddDays(-7),
            EndDate = endDate ?? today.AddDays(7),
            TargetWorkoutCount = targetWorkoutCount
        };
        db.CommunityChallenges.Add(challenge);
        await db.SaveChangesAsync();
        return challenge.Id;
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
