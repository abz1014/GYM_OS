using GymOS.Application.Common.Exceptions;
using GymOS.Application.Modules.Nutrition.Commands;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Members;
using GymOS.Domain.Nutrition;
using GymOS.Domain.Tenancy;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Nutrition;

/// <summary>
/// AddMealEntryCommand's core business rule: an entry can only be logged against a diet plan that
/// actually exists, and it must be timestamped at the moment it's logged — otherwise the calorie/
/// macro totals reports.nutrition later aggregates from ConsumedAt would silently drop it.
/// </summary>
public class AddMealEntryCommandHandlerTests : ApplicationTestBase
{
    [Fact]
    public async Task Adding_a_meal_entry_persists_it_stamped_with_the_current_time()
    {
        var (tenantId, dietPlanId, foodItemId) = await SeedAsync();
        CurrentUser.TenantId = tenantId;
        DateTimeProvider.UtcNow = new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero);

        var entryId = await SendAsync(new AddMealEntryCommand(dietPlanId, foodItemId, MealType.Breakfast, Quantity: 1.5m));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var entry = await db.MealEntries.SingleAsync(e => e.Id == entryId);
        entry.DietPlanId.ShouldBe(dietPlanId);
        entry.FoodItemId.ShouldBe(foodItemId);
        entry.Quantity.ShouldBe(1.5m);
        entry.ConsumedAt.ShouldBe(new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task Adding_a_meal_entry_to_a_nonexistent_diet_plan_is_rejected()
    {
        var (tenantId, _, foodItemId) = await SeedAsync();
        CurrentUser.TenantId = tenantId;

        var act = () => SendAsync(new AddMealEntryCommand(Guid.NewGuid(), foodItemId, MealType.Lunch, 1m));

        await Should.ThrowAsync<NotFoundException>(act);
    }

    private async Task<(Guid TenantId, Guid DietPlanId, Guid FoodItemId)> SeedAsync()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);

        var branch = new Branch { TenantId = tenant.Id, Name = "Main", AddressLine = "1 Main St", City = "City", Country = "US" };
        db.Branches.Add(branch);

        var member = new Member
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12],
            FirstName = "Test",
            LastName = "Member",
            Email = $"{Guid.NewGuid():N}@example.com",
            JoinDate = new DateOnly(2025, 1, 1),
            QrCodeToken = Guid.NewGuid().ToString("N")
        };
        db.Members.Add(member);

        var dietPlan = new DietPlan { TenantId = member.TenantId, MemberId = member.Id, Name = "Cutting Phase", StartDate = new DateOnly(2026, 1, 1) };
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
        return (tenant.Id, dietPlan.Id, foodItem.Id);
    }
}
