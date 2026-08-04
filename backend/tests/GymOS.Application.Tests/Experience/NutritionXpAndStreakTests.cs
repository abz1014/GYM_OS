using GymOS.Application.Modules.Experience.Queries;
using GymOS.Application.Modules.Nutrition.Commands;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Attendance;
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

namespace GymOS.Application.Tests.Experience;

/// <summary>
/// Slice 4: logging a meal rewards nutrition consistency once per day (via MealLoggedEvent), and the
/// streaks query reports attendance/workout/nutrition weekly streaks from the member's own history.
/// </summary>
public class NutritionXpAndStreakTests : ApplicationTestBase
{
    [Fact]
    public async Task Logging_meals_awards_nutrition_xp_once_per_day()
    {
        var ctx = await SeedAsync();
        SetAuthenticatedAs(ctx.TenantId, ctx.UserId);

        await SendAsync(new AddMealEntryCommand(ctx.DietPlanId, ctx.FoodItemId, MealType.Lunch, 1m));
        await SendAsync(new AddMealEntryCommand(ctx.DietPlanId, ctx.FoodItemId, MealType.Dinner, 1m)); // same day

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var nutritionXp = await db.XpTransactions
            .Where(t => t.MemberId == ctx.MemberId && t.Reason == XpReason.NutritionAdherence)
            .ToListAsync();

        nutritionXp.ShouldHaveSingleItem(); // both meals are the same day -> one award
        nutritionXp[0].Amount.ShouldBe(XpPolicy.AwardFor(XpReason.NutritionAdherence)); // 15
    }

    [Fact]
    public async Task Streaks_query_reports_attendance_workout_and_nutrition_weeks()
    {
        var ctx = await SeedAsync();
        SetAuthenticatedAs(ctx.TenantId, ctx.UserId);
        await SeedThisWeekActivityAsync(ctx);

        var streaks = await SendAsync(new GetMyStreaksQuery());

        streaks.AttendanceWeeks.ShouldBeGreaterThanOrEqualTo(1);
        streaks.WorkoutWeeks.ShouldBeGreaterThanOrEqualTo(1);
        streaks.NutritionWeeks.ShouldBeGreaterThanOrEqualTo(1);
    }

    private void SetAuthenticatedAs(Guid tenantId, Guid userId)
    {
        CurrentUser.TenantId = tenantId;
        CurrentUser.UserId = userId;
        CurrentUser.IsAuthenticated = true;
    }

    private async Task SeedThisWeekActivityAsync((Guid TenantId, Guid BranchId, Guid MemberId, Guid DietPlanId, Guid FoodItemId, Guid UserId) ctx)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        // Use the same clock the query reads its "today" from, so the activity lands in the current week.
        var now = DateTimeProvider.UtcNow;

        db.AttendanceRecords.Add(new AttendanceRecord
        {
            TenantId = ctx.TenantId, BranchId = ctx.BranchId, MemberId = ctx.MemberId, CheckInAt = now, Method = AttendanceMethod.Manual
        });
        db.WorkoutLogs.Add(new WorkoutLog { MemberId = ctx.MemberId, LoggedAt = now });
        db.MealEntries.Add(new MealEntry { DietPlanId = ctx.DietPlanId, FoodItemId = ctx.FoodItemId, MealType = MealType.Lunch, Quantity = 1m, ConsumedAt = now });

        await db.SaveChangesAsync();
    }

    private async Task<(Guid TenantId, Guid BranchId, Guid MemberId, Guid DietPlanId, Guid FoodItemId, Guid UserId)> SeedAsync()
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

        // Linked to the user, so MyMemberResolver (used by the streaks query) resolves to this member.
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

        var dietPlan = new DietPlan { MemberId = member.Id, Name = "Plan", StartDate = new DateOnly(2025, 1, 1) };
        db.DietPlans.Add(dietPlan);

        var food = new FoodItem
        {
            TenantId = tenant.Id,
            Name = "Chicken Breast",
            CaloriesPerServing = 165,
            ProteinG = 31,
            CarbsG = 0,
            FatG = 3.6m,
            ServingSizeDescription = "100g"
        };
        db.FoodItems.Add(food);

        await db.SaveChangesAsync();
        return (tenant.Id, branch.Id, member.Id, dietPlan.Id, food.Id, user.Id);
    }
}
