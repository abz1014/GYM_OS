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
/// Slice 6: the recommendation engine synthesizes typed nudges from the member's own real history —
/// a plateau alert from progressive-overload signals, weekly focus from mastery, exercise substitution
/// from skill-tree progress — and a trainer's active plan suppresses the self-directed "what to train"
/// ones (WeeklyFocus, ExerciseSubstitution) in favor of a single TrainerPlanActive nudge.
/// </summary>
public class RecommendationEngineTests : ApplicationTestBase
{
    [Fact]
    public async Task Trainer_plan_active_suppresses_weekly_focus_and_exercise_substitution()
    {
        var ctx = await SeedAsync();
        SetAuthenticatedAs(ctx.TenantId, ctx.UserId);

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            var now = DateTimeProvider.UtcNow;

            // A weak-mastery muscle group so WeeklyFocus WOULD fire if not for the trainer plan below.
            db.ExerciseMasteries.Add(new ExerciseMastery
            {
                TenantId = ctx.TenantId, MemberId = ctx.MemberId, ExerciseId = ctx.ExerciseId,
                Sessions = 1, TotalVolume = 500m, BestWeightKg = 50m, LastTrainedAt = now, UpdatedAt = now
            });

            // A skill tree with its first node already cleared, so ExerciseSubstitution WOULD also fire
            // if not for the trainer plan below.
            var nextExercise = new Exercise { TenantId = ctx.TenantId, Name = "Overhead Press", MuscleGroup = "Shoulders" };
            db.Exercises.Add(nextExercise);
            var tree = new SkillTree { TenantId = ctx.TenantId, Name = "Push Strength Progression", MuscleGroup = "Chest" };
            db.SkillTrees.Add(tree);
            await db.SaveChangesAsync();

            db.SkillNodes.Add(new SkillNode { SkillTreeId = tree.Id, ExerciseId = ctx.ExerciseId, OrderIndex = 0, MinReps = 6, UnlockExplanation = "Bench press node." });
            db.SkillNodes.Add(new SkillNode { SkillTreeId = tree.Id, ExerciseId = nextExercise.Id, OrderIndex = 1, MinReps = 6, UnlockExplanation = "Try Overhead Press next." });

            var log = new WorkoutLog { MemberId = ctx.MemberId, LoggedAt = now.AddDays(-1) };
            log.Entries.Add(new WorkoutLogEntry { ExerciseId = ctx.ExerciseId, SetsCompleted = 3, RepsCompleted = 8, WeightKg = 60m });
            db.WorkoutLogs.Add(log);

            var template = new WorkoutTemplate { TenantId = ctx.TenantId, Name = "Strength Foundations" };
            db.WorkoutTemplates.Add(template);
            await db.SaveChangesAsync();

            db.WorkoutAssignments.Add(new WorkoutAssignment
            {
                MemberId = ctx.MemberId, WorkoutTemplateId = template.Id,
                StartDate = DateOnly.FromDateTime(now.UtcDateTime).AddDays(-1), EndDate = null
            });
            await db.SaveChangesAsync();
        }

        var recommendations = await SendAsync(new GetMyRecommendationsQuery());

        recommendations.ShouldContain(r => r.Type == "TrainerPlanActive" && r.Explanation.Contains("Strength Foundations"));
        recommendations.ShouldNotContain(r => r.Type == "WeeklyFocus");
        recommendations.ShouldNotContain(r => r.Type == "ExerciseSubstitution");
    }

    [Fact]
    public async Task Plateau_alert_fires_for_a_two_session_plateau()
    {
        var ctx = await SeedAsync();
        SetAuthenticatedAs(ctx.TenantId, ctx.UserId);

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            var now = DateTimeProvider.UtcNow;

            var older = new WorkoutLog { MemberId = ctx.MemberId, LoggedAt = now.AddDays(-7) };
            older.Entries.Add(new WorkoutLogEntry { ExerciseId = ctx.ExerciseId, SetsCompleted = 3, RepsCompleted = 8, WeightKg = 60m });
            db.WorkoutLogs.Add(older);

            var newer = new WorkoutLog { MemberId = ctx.MemberId, LoggedAt = now.AddDays(-1) };
            newer.Entries.Add(new WorkoutLogEntry { ExerciseId = ctx.ExerciseId, SetsCompleted = 3, RepsCompleted = 8, WeightKg = 60m });
            db.WorkoutLogs.Add(newer);

            await db.SaveChangesAsync();
        }

        var recommendations = await SendAsync(new GetMyRecommendationsQuery());

        recommendations.ShouldContain(r => r.Type == "PlateauAlert" && r.ExerciseId == ctx.ExerciseId);
    }

    [Fact]
    public async Task Weekly_focus_recommends_the_weakest_trained_muscle_group()
    {
        var ctx = await SeedAsync();
        SetAuthenticatedAs(ctx.TenantId, ctx.UserId);

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            var now = DateTimeProvider.UtcNow;

            var strongExercise = new Exercise { TenantId = ctx.TenantId, Name = "Barbell Squat", MuscleGroup = "Legs" };
            db.Exercises.Add(strongExercise);
            await db.SaveChangesAsync();

            // ctx.ExerciseId ("Bench Press", muscle group "Chest") gets a weak, single-session mastery...
            db.ExerciseMasteries.Add(new ExerciseMastery
            {
                TenantId = ctx.TenantId, MemberId = ctx.MemberId, ExerciseId = ctx.ExerciseId,
                Sessions = 1, TotalVolume = 500m, BestWeightKg = 50m, LastTrainedAt = now, UpdatedAt = now
            });
            // ...while Legs gets a much deeper, well-trained mastery.
            db.ExerciseMasteries.Add(new ExerciseMastery
            {
                TenantId = ctx.TenantId, MemberId = ctx.MemberId, ExerciseId = strongExercise.Id,
                Sessions = 5, TotalVolume = 5000m, BestWeightKg = 100m, LastTrainedAt = now, UpdatedAt = now
            });
            await db.SaveChangesAsync();
        }

        var recommendations = await SendAsync(new GetMyRecommendationsQuery());

        recommendations.ShouldContain(r => r.Type == "WeeklyFocus" && r.Title.Contains("Chest"));
    }

    [Fact]
    public async Task Exercise_substitution_recommends_the_next_skill_node_once_the_current_one_is_cleared()
    {
        var ctx = await SeedAsync();
        SetAuthenticatedAs(ctx.TenantId, ctx.UserId);

        Guid nextExerciseId;
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            var now = DateTimeProvider.UtcNow;

            var nextExercise = new Exercise { TenantId = ctx.TenantId, Name = "Overhead Press", MuscleGroup = "Shoulders" };
            db.Exercises.Add(nextExercise);
            await db.SaveChangesAsync();
            nextExerciseId = nextExercise.Id;

            var tree = new SkillTree { TenantId = ctx.TenantId, Name = "Push Strength Progression", MuscleGroup = "Chest" };
            db.SkillTrees.Add(tree);
            await db.SaveChangesAsync();

            db.SkillNodes.Add(new SkillNode { SkillTreeId = tree.Id, ExerciseId = ctx.ExerciseId, OrderIndex = 0, MinReps = 6, UnlockExplanation = "Bench press node." });
            db.SkillNodes.Add(new SkillNode { SkillTreeId = tree.Id, ExerciseId = nextExercise.Id, OrderIndex = 1, MinReps = 6, UnlockExplanation = "Try Overhead Press next." });
            await db.SaveChangesAsync();

            // Clears node 0 (ctx.ExerciseId / Bench Press, MinReps 6) with an 8-rep set.
            var log = new WorkoutLog { MemberId = ctx.MemberId, LoggedAt = now.AddDays(-1) };
            log.Entries.Add(new WorkoutLogEntry { ExerciseId = ctx.ExerciseId, SetsCompleted = 3, RepsCompleted = 8, WeightKg = 60m });
            db.WorkoutLogs.Add(log);
            await db.SaveChangesAsync();
        }

        var recommendations = await SendAsync(new GetMyRecommendationsQuery());

        recommendations.ShouldContain(r => r.Type == "ExerciseSubstitution" && r.ExerciseId == nextExerciseId);
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
