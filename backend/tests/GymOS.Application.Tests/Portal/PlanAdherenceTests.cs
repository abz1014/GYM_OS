using GymOS.Application.Common;
using GymOS.Application.Modules.Nutrition.Commands;
using GymOS.Application.Modules.Portal.Commands;
using GymOS.Application.Modules.Portal.Queries;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Experience;
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
/// The one-tap adherence confirmation, and the two things it exists to keep alive.
///
/// The member's nutrition screen became a prescription to read rather than a food diary to fill in.
/// Two things were resting on meal logging and would have broken silently: NutritionAdherence XP,
/// whose only award site was MealLoggedEvent, and CoachingPolicy's compliance percentage, which
/// returns 0% — not null — for a member who has a plan and logs nothing.
///
/// The dedupe test is the load-bearing one. Both paths award through the same derived day key and
/// the same source type on purpose; a separate source type would have looked tidier and paid a
/// member who did both thirty XP for one day.
/// </summary>
public class PlanAdherenceTests : ApplicationTestBase
{
    [Fact]
    public async Task Confirming_adherence_awards_nutrition_xp_once_per_day()
    {
        var s = await SeedAsync();

        await SendAsync(new LogMyPlanAdherenceCommand(null));
        await SendAsync(new LogMyPlanAdherenceCommand("second tap, same day"));

        var xp = await NutritionXpAsync(s.MemberId);
        xp.ShouldHaveSingleItem().Amount.ShouldBe(XpPolicy.AwardFor(XpReason.NutritionAdherence));
    }

    [Fact]
    public async Task A_second_tap_returns_the_existing_row_rather_than_creating_another()
    {
        var s = await SeedAsync();

        var first = await SendAsync(new LogMyPlanAdherenceCommand(null));
        var second = await SendAsync(new LogMyPlanAdherenceCommand(null));

        second.ShouldBe(first);

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        (await db.PlanAdherenceLogs.Where(a => a.MemberId == s.MemberId).ToListAsync()).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Logging_a_meal_and_confirming_adherence_on_the_same_day_pays_fifteen_once()
    {
        /*
         * The regression this file exists for. MemberXpService dedupes on
         * (MemberId, SourceType, SourceId, Reason) — so the two handlers share BOTH the source type
         * and the `{member}:nutrition:{date}` key. Introducing XpSourceType.NutritionAdherence would
         * have read better and split the idempotency key in two, paying thirty for one day.
         */
        var s = await SeedAsync();

        await SendAsync(new AddMealEntryCommand(s.PlanId, s.FoodItemId, MealType.Lunch, 1m));
        await SendAsync(new LogMyPlanAdherenceCommand(null));

        var xp = await NutritionXpAsync(s.MemberId);
        xp.ShouldHaveSingleItem();
        xp[0].Amount.ShouldBe(XpPolicy.AwardFor(XpReason.NutritionAdherence));
    }

    [Fact]
    public async Task The_xp_day_key_is_the_utc_day_so_it_matches_the_meal_path_in_any_timezone()
    {
        /*
         * The bug this pins, which the dedupe test above could not see.
         *
         * AddMealEntryCommand keys its event on the UTC date. The adherence command originally keyed
         * on the member's GYM-CLOCK date. Both hash "{member}:nutrition:{date}" into the idempotency
         * key — so for any gym not on UTC, a tick and a meal on the same evening produced two
         * different strings, the dedupe missed, and the member earned thirty for one day. The test
         * above passes either way because the harness clock IS UTC, which is precisely why a test
         * that asserts the KEY rather than the outcome is the one worth having.
         *
         * Asserting on the stored SourceId directly: it is the derived guid, and it must equal the
         * hash of the UTC day whatever timezone the branch is in.
         */
        var s = await SeedAsync(branchTimeZone: "Pacific/Kiritimati"); // UTC+14, the furthest ahead there is

        await SendAsync(new LogMyPlanAdherenceCommand(null));

        var utcDay = DateOnly.FromDateTime(DateTimeProvider.UtcNow.UtcDateTime);
        var expected = DeterministicGuid.From($"{s.MemberId}:nutrition:{utcDay:yyyy-MM-dd}");

        var xp = await NutritionXpAsync(s.MemberId);
        xp.ShouldHaveSingleItem().SourceId.ShouldBe(expected);
    }

    [Fact]
    public async Task A_member_with_no_active_plan_cannot_confirm_adherence_to_nothing()
    {
        var s = await SeedAsync(planEndedDaysAgo: 3);

        await Should.ThrowAsync<Exception>(async () => await SendAsync(new LogMyPlanAdherenceCommand(null)));
    }

    [Fact]
    public async Task The_prescription_reports_the_confirmation_so_the_screen_stops_offering_it()
    {
        var s = await SeedAsync();

        (await SendAsync(new GetMyNutritionPrescriptionQuery())).ConfirmedToday.ShouldBeFalse();

        await SendAsync(new LogMyPlanAdherenceCommand(null));

        var after = await SendAsync(new GetMyNutritionPrescriptionQuery());
        after.ConfirmedToday.ShouldBeTrue();
        after.DaysConfirmedInWindow.ShouldBe(1);
    }

    [Fact]
    public async Task The_prescription_carries_the_plan_the_notes_and_this_weeks_guidance()
    {
        var s = await SeedAsync();
        await AddGuidanceAsync(s, GuidanceCadence.Weekly, daysAgo: 1, "Carbs down on rest days");
        await AddGuidanceAsync(s, GuidanceCadence.Weekly, daysAgo: 9, "Last week: hit protein first");
        await AddGuidanceAsync(s, GuidanceCadence.Monthly, daysAgo: 4, "Cutting block, week 2 of 4");

        var p = await SendAsync(new GetMyNutritionPrescriptionQuery());

        p.PlanName.ShouldBe("Cutting Phase");
        p.Notes.ShouldBe("Protein at every meal. Water before coffee.");
        p.TargetCalories.ShouldBe(2200m);

        // The newest weekly note whose date has arrived — not the older one, and not the monthly.
        p.ThisWeek.ShouldNotBeNull().Title.ShouldBe("Carbs down on rest days");
        p.ThisMonth.ShouldNotBeNull().Title.ShouldBe("Cutting block, week 2 of 4");

        // The superseded note becomes history, which is what makes the plan read as progressing.
        p.History.ShouldHaveSingleItem().Title.ShouldBe("Last week: hit protein first");
    }

    [Fact]
    public async Task Guidance_dated_in_the_future_is_not_shown_early()
    {
        // A nutritionist writing Monday's note on Friday must not have it appear on Friday.
        var s = await SeedAsync();
        await AddGuidanceAsync(s, GuidanceCadence.Weekly, daysAgo: 1, "This week");
        await AddGuidanceAsync(s, GuidanceCadence.Weekly, daysAgo: -3, "Next week");

        var p = await SendAsync(new GetMyNutritionPrescriptionQuery());

        p.ThisWeek.ShouldNotBeNull().Title.ShouldBe("This week");
        p.History.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_member_with_no_plan_gets_nulls_rather_than_an_empty_plan_full_of_zeroes()
    {
        await SeedAsync(planEndedDaysAgo: 3);

        var p = await SendAsync(new GetMyNutritionPrescriptionQuery());

        p.PlanName.ShouldBeNull();
        p.TargetCalories.ShouldBeNull();
        p.ConfirmedToday.ShouldBeFalse();
    }

    // ---- harness ----

    private async Task<List<XpTransaction>> NutritionXpAsync(Guid memberId)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        return await db.XpTransactions
            .Where(t => t.MemberId == memberId && t.Reason == XpReason.NutritionAdherence)
            .ToListAsync();
    }

    private record Seeded(Guid TenantId, Guid MemberId, Guid PlanId, Guid FoodItemId);

    private async Task AddGuidanceAsync(Seeded s, GuidanceCadence cadence, int daysAgo, string title)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        db.DietPlanGuidance.Add(new DietPlanGuidance
        {
            TenantId = s.TenantId,
            DietPlanId = s.PlanId,
            Cadence = cadence,
            EffectiveFrom = DateOnly.FromDateTime(DateTimeProvider.UtcNow.UtcDateTime).AddDays(-daysAgo),
            Title = title,
            CreatedAt = DateTimeProvider.UtcNow
        });

        await db.SaveChangesAsync();
    }

    private async Task<Seeded> SeedAsync(int? planEndedDaysAgo = null, string? branchTimeZone = null)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var todayUtc = DateOnly.FromDateTime(DateTimeProvider.UtcNow.UtcDateTime);

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);

        var branch = new Branch { TenantId = tenant.Id, Name = "Main", AddressLine = "1 Main St", City = "City", Country = "US" };
        if (branchTimeZone is not null) { branch.TimeZone = branchTimeZone; }
        db.Branches.Add(branch);

        var user = new User
        {
            TenantId = tenant.Id,
            Email = $"{Guid.NewGuid():N}@example.com",
            PasswordHash = "unused-in-this-test",
            FirstName = "Hungry",
            LastName = "Member"
        };
        db.Users.Add(user);

        var member = new Member
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            UserId = user.Id,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12],
            FirstName = "Hungry",
            LastName = "Member",
            Email = $"{Guid.NewGuid():N}@example.com",
            JoinDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = MemberStatus.Active,
            QrCodeToken = Guid.NewGuid().ToString("N")
        };
        db.Members.Add(member);

        var plan = new DietPlan
        {
            TenantId = tenant.Id,
            MemberId = member.Id,
            Name = "Cutting Phase",
            Notes = "Protein at every meal. Water before coffee.",
            TargetCalories = 2200m,
            TargetProteinG = 180m,
            StartDate = todayUtc.AddDays(-30),
            EndDate = planEndedDaysAgo is null ? todayUtc.AddDays(30) : todayUtc.AddDays(-planEndedDaysAgo.Value)
        };
        db.DietPlans.Add(plan);

        var food = new FoodItem { TenantId = tenant.Id, Name = "Chicken breast", CaloriesPerServing = 165 };
        db.FoodItems.Add(food);

        await db.SaveChangesAsync();

        CurrentUser.TenantId = tenant.Id;
        CurrentUser.UserId = user.Id;
        CurrentUser.IsAuthenticated = true;

        return new Seeded(tenant.Id, member.Id, plan.Id, food.Id);
    }
}
