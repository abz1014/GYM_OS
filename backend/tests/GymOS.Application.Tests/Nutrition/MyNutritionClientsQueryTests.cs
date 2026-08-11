using GymOS.Application.Modules.Nutrition.Queries;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Members;
using GymOS.Domain.Nutrition;
using GymOS.Domain.Tenancy;
using GymOS.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Nutrition;

/// <summary>
/// The nutritionist's roster, which exists only because DietPlan.CreatedByUserId was already being
/// written and never read. No assignment table backs it, so the tests below are what stops a future
/// refactor from quietly dropping the author filter and handing one nutritionist the whole gym.
///
/// The rules worth pinning are all about who a row belongs to and whether it is still live:
/// authorship is the boundary, a lapsed plan is still a client, and "unserved" is a fact about the
/// gym rather than about the caller.
/// </summary>
public class MyNutritionClientsQueryTests : ApplicationTestBase
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 6, 15);

    /// <summary>MealEntry carries a real FK to FoodItem, so a meal needs something to have been eaten.</summary>
    private Guid _foodItemId;

    [Fact]
    public async Task Only_the_members_this_user_wrote_a_plan_for_are_on_their_roster()
    {
        var s = await SeedAsync();
        await AddPlanAsync(s.TenantId, s.MemberA, s.MeId, "Mine", Today.AddDays(-10));
        await AddPlanAsync(s.TenantId, s.MemberB, s.ColleagueId, "Theirs", Today.AddDays(-10));

        var result = await SendAsync(new GetMyNutritionClientsQuery());

        result.Clients.Count.ShouldBe(1);
        result.Clients[0].MemberId.ShouldBe(s.MemberA);
        result.Clients[0].PlanName.ShouldBe("Mine");
    }

    [Fact]
    public async Task A_plan_with_no_recorded_author_belongs_to_nobody()
    {
        // CreatedByUserId is nullable, and an imported or seeded plan may carry no author. Those must
        // not fall to whoever opens the screen first.
        var s = await SeedAsync();
        await AddPlanAsync(s.TenantId, s.MemberA, authorId: null, "Orphan", Today.AddDays(-10));

        var result = await SendAsync(new GetMyNutritionClientsQuery());

        result.Clients.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_member_prescribed_twice_is_one_client_showing_the_newer_plan()
    {
        var s = await SeedAsync();
        await AddPlanAsync(s.TenantId, s.MemberA, s.MeId, "Winter cut", Today.AddDays(-200), Today.AddDays(-120));
        await AddPlanAsync(s.TenantId, s.MemberA, s.MeId, "Summer maintenance", Today.AddDays(-20));

        var result = await SendAsync(new GetMyNutritionClientsQuery());

        result.Clients.Count.ShouldBe(1);
        result.Clients[0].PlanName.ShouldBe("Summer maintenance");
    }

    [Fact]
    public async Task A_plan_that_has_run_out_keeps_the_member_on_the_roster_and_puts_them_first()
    {
        // The whole reason the roster is not filtered to active plans. A member whose prescription
        // lapsed is the clearest piece of work on the screen; dropping them is how someone quietly
        // stops being anyone's client.
        var s = await SeedAsync();
        await AddPlanAsync(s.TenantId, s.MemberA, s.MeId, "Still running", Today.AddDays(-10));
        await AddPlanAsync(s.TenantId, s.MemberB, s.MeId, "Expired last month", Today.AddDays(-90), Today.AddDays(-30));

        var result = await SendAsync(new GetMyNutritionClientsQuery());

        result.Clients.Count.ShouldBe(2);
        result.Clients[0].MemberId.ShouldBe(s.MemberB);
        result.Clients[0].PlanIsActive.ShouldBeFalse();
        result.Clients[1].PlanIsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task A_plan_that_has_not_started_yet_is_not_active_either()
    {
        var s = await SeedAsync();
        await AddPlanAsync(s.TenantId, s.MemberA, s.MeId, "Starts Monday", Today.AddDays(3));

        var result = await SendAsync(new GetMyNutritionClientsQuery());

        result.Clients.Single().PlanIsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Meals_are_counted_for_the_last_seven_days_only_and_the_latest_one_is_reported()
    {
        var s = await SeedAsync();
        var planId = await AddPlanAsync(s.TenantId, s.MemberA, s.MeId, "Cutting", Today.AddDays(-60));

        await AddMealAsync(s.TenantId, planId, Now.AddDays(-1));
        await AddMealAsync(s.TenantId, planId, Now.AddDays(-3));
        await AddMealAsync(s.TenantId, planId, Now.AddDays(-30));

        var result = await SendAsync(new GetMyNutritionClientsQuery());

        var client = result.Clients.Single();
        client.MealsLoggedLast7Days.ShouldBe(2);
        client.LastMealLoggedAt.ShouldBe(Now.AddDays(-1));
    }

    [Fact]
    public async Task A_member_who_has_never_logged_reports_null_rather_than_a_zero_date()
    {
        // "Never logged" and "logged at the epoch" render very differently, and only one of them is
        // true. The ordering below relies on MinValue as a sort key, which is exactly the sort of
        // sentinel that leaks into a DTO if nobody checks.
        var s = await SeedAsync();
        await AddPlanAsync(s.TenantId, s.MemberA, s.MeId, "Never opened", Today.AddDays(-5));

        var result = await SendAsync(new GetMyNutritionClientsQuery());

        var client = result.Clients.Single();
        client.LastMealLoggedAt.ShouldBeNull();
        client.MealsLoggedLast7Days.ShouldBe(0);
    }

    [Fact]
    public async Task The_quietest_client_is_listed_before_the_one_who_logged_today()
    {
        var s = await SeedAsync();
        var busy = await AddPlanAsync(s.TenantId, s.MemberA, s.MeId, "Engaged", Today.AddDays(-5));
        await AddPlanAsync(s.TenantId, s.MemberB, s.MeId, "Gone quiet", Today.AddDays(-5));
        await AddMealAsync(s.TenantId, busy, Now.AddHours(-2));

        var result = await SendAsync(new GetMyNutritionClientsQuery());

        result.Clients[0].MemberId.ShouldBe(s.MemberB);
        result.Clients[1].MemberId.ShouldBe(s.MemberA);
    }

    [Fact]
    public async Task Members_a_colleague_is_already_feeding_do_not_count_as_unserved()
    {
        // The count answers "how much of the gym has no plan", which is a fact about the gym. Scoping
        // it to the caller's own plans would report every one of a colleague's clients as unserved.
        var s = await SeedAsync();
        await AddPlanAsync(s.TenantId, s.MemberA, s.MeId, "Mine", Today.AddDays(-10));
        await AddPlanAsync(s.TenantId, s.MemberB, s.ColleagueId, "Theirs", Today.AddDays(-10));

        var result = await SendAsync(new GetMyNutritionClientsQuery());

        // Three active members were seeded; two hold a current plan between the two nutritionists.
        result.ActiveMembersWithoutAPlan.ShouldBe(1);
    }

    [Fact]
    public async Task A_member_whose_plan_expired_counts_as_unserved_again()
    {
        var s = await SeedAsync();
        await AddPlanAsync(s.TenantId, s.MemberA, s.MeId, "Expired", Today.AddDays(-90), Today.AddDays(-30));

        var result = await SendAsync(new GetMyNutritionClientsQuery());

        // Still on the roster — and back in the unserved figure, because nothing is prescribed now.
        result.Clients.Single().PlanIsActive.ShouldBeFalse();
        result.ActiveMembersWithoutAPlan.ShouldBe(3);
    }

    [Fact]
    public async Task An_unauthenticated_caller_gets_nothing_rather_than_everyone_elses_orphans()
    {
        var s = await SeedAsync();
        await AddPlanAsync(s.TenantId, s.MemberA, authorId: null, "Orphan", Today.AddDays(-10));
        CurrentUser.UserId = null;

        var result = await SendAsync(new GetMyNutritionClientsQuery());

        result.Clients.ShouldBeEmpty();
        result.ActiveMembersWithoutAPlan.ShouldBe(0);
    }

    private async Task<Guid> AddPlanAsync(
        Guid tenantId, Guid memberId, Guid? authorId, string name, DateOnly start, DateOnly? end = null)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var plan = new DietPlan
        {
            TenantId = tenantId,
            MemberId = memberId,
            CreatedByUserId = authorId,
            Name = name,
            StartDate = start,
            EndDate = end
        };
        db.DietPlans.Add(plan);
        await db.SaveChangesAsync();
        return plan.Id;
    }

    private async Task AddMealAsync(Guid tenantId, Guid dietPlanId, DateTimeOffset consumedAt)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        db.MealEntries.Add(new MealEntry
        {
            TenantId = tenantId,
            DietPlanId = dietPlanId,
            FoodItemId = _foodItemId,
            MealType = MealType.Lunch,
            Quantity = 1m,
            ConsumedAt = consumedAt
        });
        await db.SaveChangesAsync();
    }

    private async Task<(Guid TenantId, Guid MeId, Guid ColleagueId, Guid MemberA, Guid MemberB)> SeedAsync()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);

        var branch = new Branch { TenantId = tenant.Id, Name = "Main", AddressLine = "1 Main St", City = "City", Country = "US" };
        db.Branches.Add(branch);

        var members = new List<Member>();
        foreach (var name in new[] { "Ann", "Ben", "Cara" })
        {
            var member = new Member
            {
                TenantId = tenant.Id,
                BranchId = branch.Id,
                MemberCode = $"MBR-{Guid.NewGuid():N}"[..12],
                FirstName = name,
                LastName = "Member",
                Email = $"{Guid.NewGuid():N}@example.com",
                JoinDate = new DateOnly(2026, 1, 1),
                Status = MemberStatus.Active,
                QrCodeToken = Guid.NewGuid().ToString("N")
            };
            members.Add(member);
            db.Members.Add(member);
        }

        var foodItem = new FoodItem
        {
            TenantId = tenant.Id,
            Name = "Oats",
            CaloriesPerServing = 380,
            ProteinG = 13,
            CarbsG = 67,
            FatG = 7,
            ServingSizeDescription = "100g"
        };
        db.FoodItems.Add(foodItem);

        await db.SaveChangesAsync();
        _foodItemId = foodItem.Id;

        CurrentUser.TenantId = tenant.Id;
        CurrentUser.UserId = Guid.NewGuid();
        CurrentUser.IsAuthenticated = true;
        DateTimeProvider.UtcNow = Now;

        return (tenant.Id, CurrentUser.UserId!.Value, Guid.NewGuid(), members[0].Id, members[1].Id);
    }
}
