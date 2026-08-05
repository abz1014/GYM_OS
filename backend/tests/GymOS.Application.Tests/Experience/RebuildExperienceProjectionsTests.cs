using GymOS.Application.Modules.Experience.Commands;
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
/// Slice 10: the projection rebuild recomputes MemberProgression and ExerciseMastery straight from
/// their append-only sources (XpTransaction, WorkoutLogEntries) — proven by seeding the sources
/// directly (bypassing MemberXpService/WorkoutProgressionService entirely, the same way a real drift
/// or a rule change would leave things), then asserting the rebuild converges on the correct values,
/// backfills achievements the bypassed event pipeline never triggered, and is idempotent.
/// </summary>
public class RebuildExperienceProjectionsTests : ApplicationTestBase
{
    [Fact]
    public async Task Rebuild_corrects_a_stale_member_progression_from_the_xp_ledger()
    {
        var ctx = await SeedAsync();
        SetAuthenticatedAs(ctx.TenantId, ctx.UserId);

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            db.XpTransactions.Add(new XpTransaction
            {
                TenantId = ctx.TenantId, MemberId = ctx.MemberId, Amount = 50, Reason = XpReason.WorkoutCompleted,
                SourceType = XpSourceType.WorkoutLog, SourceId = Guid.NewGuid(), OccurredAt = DateTimeProvider.UtcNow
            });
            db.XpTransactions.Add(new XpTransaction
            {
                TenantId = ctx.TenantId, MemberId = ctx.MemberId, Amount = 100, Reason = XpReason.GoalCompleted,
                SourceType = XpSourceType.MemberGoal, SourceId = Guid.NewGuid(), OccurredAt = DateTimeProvider.UtcNow
            });

            // Stale/drifted projection — nothing like the true 150 XP ledger sum above.
            var progression = new MemberProgression { TenantId = ctx.TenantId, MemberId = ctx.MemberId, UpdatedAt = DateTimeProvider.UtcNow };
            progression.SetTotalXp(9999);
            db.MemberProgressions.Add(progression);

            await db.SaveChangesAsync();
        }

        var result = await SendAsync(new RebuildExperienceProjectionsCommand());

        result.ProgressionsRebuilt.ShouldBe(1);

        using var verifyScope = CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var rebuilt = await verifyDb.MemberProgressions.FirstAsync(p => p.MemberId == ctx.MemberId);
        rebuilt.TotalXp.ShouldBe(150);
        rebuilt.Level.ShouldBe(XpPolicy.LevelForXp(150).Level);
    }

    [Fact]
    public async Task Rebuild_creates_a_missing_progression_row_when_the_ledger_has_xp()
    {
        var ctx = await SeedAsync();
        SetAuthenticatedAs(ctx.TenantId, ctx.UserId);

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            db.XpTransactions.Add(new XpTransaction
            {
                TenantId = ctx.TenantId, MemberId = ctx.MemberId, Amount = 20, Reason = XpReason.GymVisit,
                SourceType = XpSourceType.Attendance, SourceId = Guid.NewGuid(), OccurredAt = DateTimeProvider.UtcNow
            });
            await db.SaveChangesAsync();
        }

        await SendAsync(new RebuildExperienceProjectionsCommand());

        using var verifyScope = CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var created = await verifyDb.MemberProgressions.FirstOrDefaultAsync(p => p.MemberId == ctx.MemberId);
        created.ShouldNotBeNull();
        created!.TotalXp.ShouldBe(20);
    }

    [Fact]
    public async Task Rebuild_leaves_no_progression_row_for_a_member_who_never_earned_xp()
    {
        var ctx = await SeedAsync();
        SetAuthenticatedAs(ctx.TenantId, ctx.UserId);

        await SendAsync(new RebuildExperienceProjectionsCommand());

        using var verifyScope = CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        (await verifyDb.MemberProgressions.AnyAsync(p => p.MemberId == ctx.MemberId)).ShouldBeFalse();
    }

    [Fact]
    public async Task Rebuild_recomputes_exercise_mastery_from_workout_history()
    {
        var ctx = await SeedAsync();
        SetAuthenticatedAs(ctx.TenantId, ctx.UserId);

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            db.WorkoutLogs.Add(new WorkoutLog
            {
                MemberId = ctx.MemberId, LoggedAt = DateTimeProvider.UtcNow.AddDays(-7),
                Entries = [new WorkoutLogEntry { ExerciseId = ctx.ExerciseId, SetsCompleted = 3, RepsCompleted = 8, WeightKg = 60m }]
            });
            db.WorkoutLogs.Add(new WorkoutLog
            {
                MemberId = ctx.MemberId, LoggedAt = DateTimeProvider.UtcNow,
                Entries = [new WorkoutLogEntry { ExerciseId = ctx.ExerciseId, SetsCompleted = 3, RepsCompleted = 8, WeightKg = 65m }]
            });
            await db.SaveChangesAsync();
        }

        var result = await SendAsync(new RebuildExperienceProjectionsCommand());

        result.MasteryRowsRebuilt.ShouldBe(1);

        using var verifyScope = CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var mastery = await verifyDb.ExerciseMasteries.FirstAsync(m => m.MemberId == ctx.MemberId && m.ExerciseId == ctx.ExerciseId);
        mastery.Sessions.ShouldBe(2);
        mastery.TotalSets.ShouldBe(6);
        mastery.BestWeightKg.ShouldBe(65m);
    }

    [Fact]
    public async Task Rebuild_backfills_an_achievement_the_bypassed_event_pipeline_never_triggered()
    {
        var ctx = await SeedAsync();
        SetAuthenticatedAs(ctx.TenantId, ctx.UserId);

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            // Exactly the cumulative XP for level 3 (50 * 2 * 3), written straight to the ledger —
            // no MemberXpService.AwardAsync call means no MemberProgressionChangedEvent, so nothing
            // would ever evaluate achievements for this member without the rebuild's backfill pass.
            db.XpTransactions.Add(new XpTransaction
            {
                TenantId = ctx.TenantId, MemberId = ctx.MemberId, Amount = 300, Reason = XpReason.GoalCompleted,
                SourceType = XpSourceType.MemberGoal, SourceId = Guid.NewGuid(), OccurredAt = DateTimeProvider.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var result = await SendAsync(new RebuildExperienceProjectionsCommand());

        result.AchievementsBackfilled.ShouldBeGreaterThanOrEqualTo(1);

        using var verifyScope = CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        (await verifyDb.MemberAchievements.AnyAsync(a => a.MemberId == ctx.MemberId && a.Code == "level-3")).ShouldBeTrue();
    }

    [Fact]
    public async Task Rebuild_is_idempotent()
    {
        var ctx = await SeedAsync();
        SetAuthenticatedAs(ctx.TenantId, ctx.UserId);

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            db.XpTransactions.Add(new XpTransaction
            {
                TenantId = ctx.TenantId, MemberId = ctx.MemberId, Amount = 300, Reason = XpReason.GoalCompleted,
                SourceType = XpSourceType.MemberGoal, SourceId = Guid.NewGuid(), OccurredAt = DateTimeProvider.UtcNow
            });
            db.WorkoutLogs.Add(new WorkoutLog
            {
                MemberId = ctx.MemberId, LoggedAt = DateTimeProvider.UtcNow,
                Entries = [new WorkoutLogEntry { ExerciseId = ctx.ExerciseId, SetsCompleted = 3, RepsCompleted = 8, WeightKg = 60m }]
            });
            await db.SaveChangesAsync();
        }

        await SendAsync(new RebuildExperienceProjectionsCommand());
        var second = await SendAsync(new RebuildExperienceProjectionsCommand());

        // A second run finds nothing new to backfill and doesn't touch already-correct rows.
        second.AchievementsBackfilled.ShouldBe(0);

        using var verifyScope = CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        (await verifyDb.MemberProgressions.CountAsync(p => p.MemberId == ctx.MemberId)).ShouldBe(1);
        (await verifyDb.ExerciseMasteries.CountAsync(m => m.MemberId == ctx.MemberId)).ShouldBe(1);
        (await verifyDb.MemberAchievements.CountAsync(a => a.MemberId == ctx.MemberId && a.Code == "level-3")).ShouldBe(1);
    }

    private void SetAuthenticatedAs(Guid tenantId, Guid userId)
    {
        CurrentUser.TenantId = tenantId;
        CurrentUser.UserId = userId;
        CurrentUser.IsAuthenticated = true;
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
            TenantId = tenant.Id, Email = $"{Guid.NewGuid():N}@example.com", PasswordHash = "unused-in-this-test",
            FirstName = "Owner", LastName = "User"
        };
        db.Users.Add(user);
        db.UserBranchAccesses.Add(new UserBranchAccess { UserId = user.Id, BranchId = branch.Id });

        // A separate user for the member row (distinct from the staff user above, which is the one
        // that authenticates and triggers the rebuild) — Member.UserId has a real FK to Users.
        var memberUser = new User
        {
            TenantId = tenant.Id, Email = $"{Guid.NewGuid():N}@example.com", PasswordHash = "unused-in-this-test",
            FirstName = "Test", LastName = "Member"
        };
        db.Users.Add(memberUser);

        var member = new Member
        {
            TenantId = tenant.Id, BranchId = branch.Id, UserId = memberUser.Id,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12], FirstName = "Test", LastName = "Member",
            Email = $"{Guid.NewGuid():N}@example.com", JoinDate = new DateOnly(2025, 1, 1),
            QrCodeToken = Guid.NewGuid().ToString("N")
        };
        db.Members.Add(member);

        var exercise = new Exercise { TenantId = tenant.Id, Name = "Bench Press", MuscleGroup = "Chest", Equipment = "Barbell" };
        db.Exercises.Add(exercise);

        await db.SaveChangesAsync();
        return (tenant.Id, branch.Id, member.Id, exercise.Id, user.Id);
    }
}
