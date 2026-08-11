using GymOS.Application.Modules.Coaching.Queries;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Experience;
using GymOS.Domain.Identity;
using GymOS.Domain.Members;
using GymOS.Domain.Nutrition;
using GymOS.Domain.Tenancy;
using GymOS.Domain.Workouts;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Coaching;

/// <summary>
/// Slice 9: the trainer coaching dashboard's three read-models (plateaus, compliance, risks), each
/// proven both for correctness — the same signals RecoveryPolicy/ProgressiveOverloadPolicy/
/// CoachingPolicy already compute for a single member, just run across a roster — and for branch
/// isolation, since a trainer scoped to one branch must never see another branch's members.
/// </summary>
public class CoachingDashboardTests : ApplicationTestBase
{
    private static readonly DateTimeOffset Today = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero); // a Wednesday

    [Fact]
    public async Task Plateaus_returns_a_member_who_held_identical_weight_and_reps_across_two_sessions()
    {
        var ctx = await SeedTenantAsync();
        var member = await SeedMemberAsync(ctx.TenantId, ctx.BranchAId);

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            var exercise = new Exercise { TenantId = ctx.TenantId, Name = "Bench Press", MuscleGroup = "Chest", Equipment = "Barbell" };
            db.Exercises.Add(exercise);
            await db.SaveChangesAsync();

            AddWorkoutLog(db, member, exercise.Id, Today.AddDays(-7), setsCompleted: 3, repsCompleted: 8, weightKg: 60m);
            AddWorkoutLog(db, member, exercise.Id, Today, setsCompleted: 3, repsCompleted: 8, weightKg: 60m);
            await db.SaveChangesAsync();
        }

        SetAuthenticatedAs(ctx.TenantId, ctx.StaffUserId);
        DateTimeProvider.UtcNow = Today;

        var plateaus = await SendAsync(new GetCoachingPlateausQuery());

        var row = plateaus.ShouldHaveSingleItem();
        row.MemberId.ShouldBe(member);
        row.ExerciseName.ShouldBe("Bench Press");
        row.LastWeightKg.ShouldBe(60m);
        row.SuggestedNextWeightKg.ShouldNotBeNull();
    }

    [Fact]
    public async Task Plateaus_excludes_members_outside_the_callers_accessible_branches()
    {
        var ctx = await SeedTenantAsync();
        var branchBMember = await SeedMemberAsync(ctx.TenantId, ctx.BranchBId);

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            var exercise = new Exercise { TenantId = ctx.TenantId, Name = "Squat", MuscleGroup = "Legs", Equipment = "Barbell" };
            db.Exercises.Add(exercise);
            await db.SaveChangesAsync();

            AddWorkoutLog(db, branchBMember, exercise.Id, Today.AddDays(-7), 3, 8, 100m);
            AddWorkoutLog(db, branchBMember, exercise.Id, Today, 3, 8, 100m);
            await db.SaveChangesAsync();
        }

        SetAuthenticatedAs(ctx.TenantId, ctx.StaffUserId); // access to Branch A only
        DateTimeProvider.UtcNow = Today;

        var plateaus = await SendAsync(new GetCoachingPlateausQuery());

        plateaus.ShouldBeEmpty();
    }

    [Fact]
    public async Task Compliance_computes_workout_adherence_and_null_nutrition_without_a_diet_plan()
    {
        var ctx = await SeedTenantAsync();
        var member = await SeedMemberAsync(ctx.TenantId, ctx.BranchAId);

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            var exercise = new Exercise { TenantId = ctx.TenantId, Name = "Deadlift", MuscleGroup = "Back", Equipment = "Barbell" };
            db.Exercises.Add(exercise);
            await db.SaveChangesAsync();

            // Only the current week has a session -> 1 of the trailing 4 weeks = 25%.
            AddWorkoutLog(db, member, exercise.Id, Today, 3, 5, 120m);
            await db.SaveChangesAsync();
        }

        SetAuthenticatedAs(ctx.TenantId, ctx.StaffUserId);
        DateTimeProvider.UtcNow = Today;

        var compliance = await SendAsync(new GetCoachingComplianceQuery());

        var row = compliance.ShouldHaveSingleItem();
        row.MemberId.ShouldBe(member);
        row.WorkoutAdherencePercent.ShouldBe(25);
        row.NutritionAdherencePercent.ShouldBeNull(); // never had a diet plan
        row.LastMealLoggedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Compliance_reports_full_nutrition_adherence_when_every_active_plan_day_is_logged()
    {
        var ctx = await SeedTenantAsync();
        var member = await SeedMemberAsync(ctx.TenantId, ctx.BranchAId);

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            var food = new FoodItem { TenantId = ctx.TenantId, Name = "Chicken", CaloriesPerServing = 200, ProteinG = 30, CarbsG = 0, FatG = 5, ServingSizeDescription = "100g" };
            db.FoodItems.Add(food);
            var plan = new DietPlan { TenantId = ctx.TenantId, MemberId = member, Name = "Cut", StartDate = DateOnly.FromDateTime(Today.UtcDateTime).AddDays(-6) };
            db.DietPlans.Add(plan);
            await db.SaveChangesAsync();

            // A consumed meal on all 7 trailing-window days.
            for (var i = 0; i <= 6; i++)
            {
                db.MealEntries.Add(new MealEntry
                {
                    TenantId = ctx.TenantId,
                    DietPlanId = plan.Id, FoodItemId = food.Id, MealType = MealType.Lunch,
                    Quantity = 1, ConsumedAt = Today.AddDays(-i)
                });
            }
            await db.SaveChangesAsync();
        }

        SetAuthenticatedAs(ctx.TenantId, ctx.StaffUserId);
        DateTimeProvider.UtcNow = Today;

        var compliance = await SendAsync(new GetCoachingComplianceQuery());

        var row = compliance.ShouldHaveSingleItem();
        row.NutritionAdherencePercent.ShouldBe(100);
        row.LastMealLoggedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Risks_flags_overtraining_and_an_imminent_streak_break()
    {
        var ctx = await SeedTenantAsync();
        var overtrainedMember = await SeedMemberAsync(ctx.TenantId, ctx.BranchAId);
        var streakMember = await SeedMemberAsync(ctx.TenantId, ctx.BranchAId);
        var healthyMember = await SeedMemberAsync(ctx.TenantId, ctx.BranchAId);

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            var exercise = new Exercise { TenantId = ctx.TenantId, Name = "Row", MuscleGroup = "Back", Equipment = "Cable" };
            db.Exercises.Add(exercise);
            await db.SaveChangesAsync();

            // Overtrained: 6 sessions in the trailing 7 days, zero rest logged, most recent is today.
            for (var i = 0; i < 6; i++)
            {
                AddWorkoutLog(db, overtrainedMember, exercise.Id, Today.AddDays(-i), 3, 5, 40m);
            }

            // Streak member: checked in last week and the week before, but not yet this week.
            db.AttendanceRecords.Add(new GymOS.Domain.Attendance.AttendanceRecord
            {
                TenantId = ctx.TenantId, BranchId = ctx.BranchAId, MemberId = streakMember, CheckInAt = Today.AddDays(-8)
            });
            db.AttendanceRecords.Add(new GymOS.Domain.Attendance.AttendanceRecord
            {
                TenantId = ctx.TenantId, BranchId = ctx.BranchAId, MemberId = streakMember, CheckInAt = Today.AddDays(-15)
            });

            // Healthy member: checked in earlier this week, no overtraining signal.
            db.AttendanceRecords.Add(new GymOS.Domain.Attendance.AttendanceRecord
            {
                TenantId = ctx.TenantId, BranchId = ctx.BranchAId, MemberId = healthyMember, CheckInAt = Today
            });
            AddWorkoutLog(db, healthyMember, exercise.Id, Today, 3, 5, 40m);

            await db.SaveChangesAsync();
        }

        SetAuthenticatedAs(ctx.TenantId, ctx.StaffUserId);
        DateTimeProvider.UtcNow = Today;

        var risks = await SendAsync(new GetCoachingRisksQuery());

        risks.ShouldContain(r => r.MemberId == overtrainedMember && r.RiskType == "OvertrainingRisk");
        risks.ShouldContain(r => r.MemberId == streakMember && r.RiskType == "StreakBreakImminent");
        risks.ShouldNotContain(r => r.MemberId == healthyMember);
    }

    [Fact]
    public async Task Risks_excludes_members_outside_the_callers_accessible_branches()
    {
        var ctx = await SeedTenantAsync();
        var branchBMember = await SeedMemberAsync(ctx.TenantId, ctx.BranchBId);

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            var exercise = new Exercise { TenantId = ctx.TenantId, Name = "Overhead Press", MuscleGroup = "Shoulders", Equipment = "Barbell" };
            db.Exercises.Add(exercise);
            await db.SaveChangesAsync();

            for (var i = 0; i < 6; i++)
            {
                AddWorkoutLog(db, branchBMember, exercise.Id, Today.AddDays(-i), 3, 5, 30m);
            }
            await db.SaveChangesAsync();
        }

        SetAuthenticatedAs(ctx.TenantId, ctx.StaffUserId); // access to Branch A only
        DateTimeProvider.UtcNow = Today;

        var risks = await SendAsync(new GetCoachingRisksQuery());

        risks.ShouldBeEmpty();
    }

    private static void AddWorkoutLog(
        GymOsDbContext db, Guid memberId, Guid exerciseId, DateTimeOffset loggedAt, int setsCompleted, int repsCompleted, decimal weightKg)
    {
        // Tenant comes from the member the log belongs to — the only source in scope here, and the
        // same rule the production backfill uses.
        var tenantId = db.Members.IgnoreQueryFilters().Single(m => m.Id == memberId).TenantId;

        db.WorkoutLogs.Add(new WorkoutLog
        {
            TenantId = tenantId,
            MemberId = memberId,
            LoggedAt = loggedAt,
            Entries =
            [
                new WorkoutLogEntry { TenantId = tenantId, ExerciseId = exerciseId, SetsCompleted = setsCompleted, RepsCompleted = repsCompleted, WeightKg = weightKg }
            ]
        });
    }

    private void SetAuthenticatedAs(Guid tenantId, Guid userId)
    {
        CurrentUser.TenantId = tenantId;
        CurrentUser.UserId = userId;
        CurrentUser.IsAuthenticated = true;
    }

    private async Task<Guid> SeedMemberAsync(Guid tenantId, Guid branchId)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var user = new User
        {
            TenantId = tenantId, Email = $"{Guid.NewGuid():N}@example.com", PasswordHash = "unused-in-this-test",
            FirstName = "Test", LastName = "Member"
        };
        db.Users.Add(user);

        var member = new Member
        {
            TenantId = tenantId, BranchId = branchId, UserId = user.Id,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12], FirstName = "Test", LastName = "Member",
            Email = $"{Guid.NewGuid():N}@example.com", JoinDate = new DateOnly(2025, 1, 1),
            QrCodeToken = Guid.NewGuid().ToString("N")
        };
        db.Members.Add(member);

        await db.SaveChangesAsync();
        return member.Id;
    }

    private async Task<(Guid TenantId, Guid BranchAId, Guid BranchBId, Guid StaffUserId)> SeedTenantAsync()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);

        var branchA = new Branch { TenantId = tenant.Id, Name = "Branch A", AddressLine = "1 Main St", City = "City", Country = "US" };
        var branchB = new Branch { TenantId = tenant.Id, Name = "Branch B", AddressLine = "2 Main St", City = "City", Country = "US" };
        db.Branches.AddRange(branchA, branchB);

        var staffUser = new User
        {
            TenantId = tenant.Id, Email = $"{Guid.NewGuid():N}@example.com", PasswordHash = "unused-in-this-test",
            FirstName = "Trainer", LastName = "User"
        };
        db.Users.Add(staffUser);

        await db.SaveChangesAsync();

        // Access to Branch A only — every "excludes" test relies on this.
        db.UserBranchAccesses.Add(new UserBranchAccess { UserId = staffUser.Id, BranchId = branchA.Id });
        await db.SaveChangesAsync();

        return (tenant.Id, branchA.Id, branchB.Id, staffUser.Id);
    }
}
