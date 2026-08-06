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
/// The recommendation engine synthesizes typed nudges from the member's own real history — exercise
/// substitution from skill-tree progress, a volume swing, recovery advice — and a trainer's active
/// plan suppresses the self-directed "what to train" one in favour of a single TrainerPlanActive
/// nudge.
///
/// The overload alert and weakest-group focus this suite used to cover went in the Step 9 review:
/// each said, in different words, something the member was already reading on the same screen.
/// TrainingInsightPolicy carries both facts now, ranked rather than listed.
/// </summary>
public class RecommendationEngineTests : ApplicationTestBase
{
    [Fact]
    public async Task Trainer_plan_active_suppresses_exercise_substitution()
    {
        var ctx = await SeedAsync();
        SetAuthenticatedAs(ctx.TenantId, ctx.UserId);

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            var now = DateTimeProvider.UtcNow;

            // A skill tree with its first node already cleared, so ExerciseSubstitution WOULD fire if
            // not for the trainer plan below.
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
        recommendations.ShouldNotContain(r => r.Type == "ExerciseSubstitution");
    }

    [Fact]
    public async Task Nothing_here_repeats_what_another_surface_already_says()
    {
        // The Step 9 subtraction, pinned. A member two sessions into the same weight used to be told
        // "ready to add weight" here, on the same screen as the suggestion list that says it, and
        // again on the home screen. This engine is now silent on both — one fact, one place.
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

            db.ExerciseMasteries.Add(new ExerciseMastery
            {
                TenantId = ctx.TenantId, MemberId = ctx.MemberId, ExerciseId = ctx.ExerciseId,
                Sessions = 1, TotalVolume = 500m, BestWeightKg = 50m, LastTrainedAt = now, UpdatedAt = now
            });

            await db.SaveChangesAsync();
        }

        var recommendations = await SendAsync(new GetMyRecommendationsQuery());

        recommendations.ShouldNotContain(r => r.Type == "PlateauAlert");
        recommendations.ShouldNotContain(r => r.Type == "WeeklyFocus");
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
