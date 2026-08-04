using GymOS.Application.Modules.Portal.Queries;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Identity;
using GymOS.Domain.Members;
using GymOS.Domain.Nutrition;
using GymOS.Domain.Tenancy;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Portal;

/// <summary>
/// The portal's daily nutrition card must total only entries actually consumed today against
/// whichever diet plan covers today's date — a planned-but-not-eaten entry, an entry from a
/// different day, or a plan that hasn't started/has already ended must never bleed into the total.
/// </summary>
public class GetMyNutritionSummaryQueryTests : ApplicationTestBase
{
    [Fact]
    public async Task Todays_consumed_entries_are_totaled_against_the_active_plans_targets()
    {
        var (tenantId, userId, dietPlanId, foodItemId) = await SeedAsync();
        CurrentUser.TenantId = tenantId;
        CurrentUser.UserId = userId;
        CurrentUser.IsAuthenticated = true;
        var today = DateTimeProvider.UtcNow;

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            // Consumed today: 2 servings.
            db.MealEntries.Add(new MealEntry { DietPlanId = dietPlanId, FoodItemId = foodItemId, MealType = MealType.Lunch, Quantity = 2m, ConsumedAt = today });
            // Consumed yesterday — must not count toward today's total.
            db.MealEntries.Add(new MealEntry { DietPlanId = dietPlanId, FoodItemId = foodItemId, MealType = MealType.Dinner, Quantity = 5m, ConsumedAt = today.AddDays(-1) });
            // Planned but never actually eaten — must not count.
            db.MealEntries.Add(new MealEntry { DietPlanId = dietPlanId, FoodItemId = foodItemId, MealType = MealType.Snack, Quantity = 9m, ConsumedAt = null });
            await db.SaveChangesAsync();
        }

        var summary = await SendAsync(new GetMyNutritionSummaryQuery());

        summary.ActiveDietPlanName.ShouldBe("Cutting Phase");
        summary.TargetCalories.ShouldBe(2000m);
        summary.TargetProteinG.ShouldBe(180m);
        // FoodItem: 165 kcal / 31g protein / 0g carb / 3.6g fat per serving, x2 servings consumed today.
        summary.ConsumedCalories.ShouldBe(330m);
        summary.ConsumedProteinG.ShouldBe(62m);
        summary.ConsumedFatG.ShouldBe(7.2m);
    }

    [Fact]
    public async Task No_active_plan_returns_a_zeroed_summary_instead_of_throwing()
    {
        var (tenantId, userId, _, _) = await SeedAsync(planCoversToday: false);
        CurrentUser.TenantId = tenantId;
        CurrentUser.UserId = userId;
        CurrentUser.IsAuthenticated = true;

        var summary = await SendAsync(new GetMyNutritionSummaryQuery());

        summary.ActiveDietPlanName.ShouldBeNull();
        summary.TargetCalories.ShouldBeNull();
        summary.ConsumedCalories.ShouldBe(0m);
    }

    private async Task<(Guid TenantId, Guid UserId, Guid DietPlanId, Guid FoodItemId)> SeedAsync(bool planCoversToday = true)
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
            FirstName = "Portal",
            LastName = "Member"
        };
        db.Users.Add(user);

        var member = new Member
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            UserId = user.Id,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12],
            FirstName = "Portal",
            LastName = "Member",
            Email = $"{Guid.NewGuid():N}@example.com",
            JoinDate = new DateOnly(2025, 1, 1),
            QrCodeToken = Guid.NewGuid().ToString("N")
        };
        db.Members.Add(member);

        var today = DateOnly.FromDateTime(DateTimeProvider.UtcNow.UtcDateTime);
        var dietPlan = new DietPlan
        {
            MemberId = member.Id,
            Name = "Cutting Phase",
            TargetCalories = 2000m,
            TargetProteinG = 180m,
            TargetCarbsG = 150m,
            TargetFatG = 60m,
            StartDate = planCoversToday ? today.AddDays(-10) : today.AddDays(-100),
            EndDate = planCoversToday ? null : today.AddDays(-50)
        };
        db.DietPlans.Add(dietPlan);

        var foodItem = new FoodItem
        {
            TenantId = tenant.Id,
            Name = "Chicken Breast",
            CaloriesPerServing = 165,
            ProteinG = 31,
            CarbsG = 0,
            FatG = 3.6m,
            ServingSizeDescription = "100g"
        };
        db.FoodItems.Add(foodItem);

        await db.SaveChangesAsync();
        return (tenant.Id, user.Id, dietPlan.Id, foodItem.Id);
    }
}
