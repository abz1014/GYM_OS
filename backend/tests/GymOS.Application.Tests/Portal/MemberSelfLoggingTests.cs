using GymOS.Application.Common.Exceptions;
using GymOS.Application.Modules.Portal.Commands;
using GymOS.Application.Modules.Portal.Queries;
using GymOS.Application.Modules.Workouts.Commands;
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
using ValidationException = FluentValidation.ValidationException;

namespace GymOS.Application.Tests.Portal;

/// <summary>
/// Member self-logging: the data-entry surface that makes the Member Experience Engine usable by an
/// actual member rather than only by staff logging on their behalf. Covers the two things that can
/// go wrong — the cascade not firing (so logging silently earns nothing), and a member writing into
/// data that isn't theirs.
/// </summary>
public class MemberSelfLoggingTests : ApplicationTestBase
{
    [Fact]
    public async Task Logging_my_own_workout_drives_the_full_experience_cascade()
    {
        var ctx = await SeedAsync();
        AsMember(ctx);

        await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.ExerciseId, 3, 8, 100m)]));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        // The log itself is attributed to the caller, never to anyone else.
        var log = await db.WorkoutLogs.Include(l => l.Entries).SingleAsync();
        log.MemberId.ShouldBe(ctx.MemberId);
        log.Entries.ShouldHaveSingleItem().WeightKg.ShouldBe(100m);

        // ...and the whole engine reacted to it, which is the entire point of member self-logging.
        (await db.XpTransactions.AnyAsync(t => t.MemberId == ctx.MemberId && t.Reason == XpReason.WorkoutCompleted)).ShouldBeTrue();
        (await db.MemberProgressions.AnyAsync(p => p.MemberId == ctx.MemberId)).ShouldBeTrue();
        (await db.PersonalRecords.AnyAsync(r => r.MemberId == ctx.MemberId)).ShouldBeTrue();
        (await db.ExerciseMasteries.AnyAsync(m => m.MemberId == ctx.MemberId && m.ExerciseId == ctx.ExerciseId)).ShouldBeTrue();
        (await db.MemberAchievements.AnyAsync(a => a.MemberId == ctx.MemberId && a.Code == "first-workout")).ShouldBeTrue();
    }

    [Fact]
    public async Task Logging_a_workout_for_an_exercise_from_another_tenant_is_rejected()
    {
        var mine = await SeedAsync();
        var other = await SeedAsync();
        AsMember(mine);

        await Should.ThrowAsync<NotFoundException>(
            () => SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(other.ExerciseId, 3, 8, 60m)])));
    }

    [Theory]
    [InlineData(0, 8, 60)]      // no sets
    [InlineData(3, 0, 60)]      // no reps
    [InlineData(3, 8, 5000)]    // absurd weight would poison mastery/PR projections
    public async Task Nonsense_workout_entries_are_rejected(int sets, int reps, decimal weight)
    {
        var ctx = await SeedAsync();
        AsMember(ctx);

        await Should.ThrowAsync<ValidationException>(
            () => SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.ExerciseId, sets, reps, weight)])));
    }

    [Fact]
    public async Task Logging_a_meal_targets_my_own_active_plan_without_me_naming_it()
    {
        var ctx = await SeedAsync(withDietPlan: true);
        AsMember(ctx);

        await SendAsync(new LogMyMealCommand(ctx.FoodItemId, MealType.Lunch, 2m));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var entry = await db.MealEntries.Include(e => e.DietPlan).SingleAsync();
        entry.DietPlan!.MemberId.ShouldBe(ctx.MemberId);
        entry.Quantity.ShouldBe(2m);
        entry.ConsumedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Logging_a_meal_without_an_active_plan_fails_cleanly()
    {
        var ctx = await SeedAsync(withDietPlan: false);
        AsMember(ctx);

        await Should.ThrowAsync<NotFoundException>(
            () => SendAsync(new LogMyMealCommand(ctx.FoodItemId, MealType.Lunch, 1m)));
    }

    [Fact]
    public async Task A_member_cannot_write_a_meal_into_another_members_plan()
    {
        // The staff-facing AddMealEntryCommand takes a DietPlanId, which is safe for staff but would
        // be a cross-member write if handed to members. LogMyMealCommand exposes no plan id at all —
        // this test pins that: the victim's plan stays empty no matter what the attacker logs.
        var attacker = await SeedAsync(withDietPlan: true);
        var victim = await SeedAsync(withDietPlan: true);

        AsMember(attacker);
        await SendAsync(new LogMyMealCommand(attacker.FoodItemId, MealType.Dinner, 1m));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var victimEntries = await db.MealEntries.IgnoreQueryFilters()
            .Include(e => e.DietPlan)
            .Where(e => e.DietPlan!.MemberId == victim.MemberId)
            .ToListAsync();
        victimEntries.ShouldBeEmpty();
    }

    [Fact]
    public async Task Logging_water_and_measurements_is_attributed_to_me()
    {
        var ctx = await SeedAsync();
        AsMember(ctx);

        await SendAsync(new LogMyWaterCommand(750));
        await SendAsync(new LogMyMeasurementCommand(82.5m, 18m, null, null, null, null, null, "post-cut"));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        (await db.WaterLogs.SingleAsync()).MemberId.ShouldBe(ctx.MemberId);
        var measurement = await db.MemberMeasurements.SingleAsync();
        measurement.MemberId.ShouldBe(ctx.MemberId);
        measurement.WeightKg.ShouldBe(82.5m);
    }

    [Fact]
    public async Task An_empty_measurement_is_rejected_rather_than_stored_as_a_blank_row()
    {
        var ctx = await SeedAsync();
        AsMember(ctx);

        await Should.ThrowAsync<ValidationException>(
            () => SendAsync(new LogMyMeasurementCommand(null, null, null, null, null, null, null, "nothing here")));
    }

    [Fact]
    public async Task Logging_options_expose_only_my_own_tenants_catalogue()
    {
        var mine = await SeedAsync(withDietPlan: true);
        await SeedAsync(withDietPlan: true); // a second tenant whose catalogue must not appear
        AsMember(mine);

        var options = await SendAsync(new GetMyLoggingOptionsQuery());

        options.Exercises.ShouldHaveSingleItem().Id.ShouldBe(mine.ExerciseId);
        options.Foods.ShouldHaveSingleItem().Id.ShouldBe(mine.FoodItemId);
        options.ActiveDietPlanName.ShouldNotBeNull();
    }

    [Fact]
    public async Task Training_volume_returns_a_continuous_series_including_rest_days()
    {
        var ctx = await SeedAsync();
        AsMember(ctx);
        var start = DateTimeProvider.UtcNow;

        await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.ExerciseId, 3, 10, 50m)]));
        DateTimeProvider.UtcNow = start.AddDays(2);
        await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.ExerciseId, 2, 10, 60m)]));

        var series = await SendAsync(new GetMyTrainingVolumeQuery(7));

        series.Count.ShouldBe(7);                                   // one point per day, gaps included
        series.Select(p => p.Date).ShouldBeUnique();
        series.Count(p => p.VolumeKg > 0).ShouldBe(2);              // only the two training days
        series.Sum(p => p.VolumeKg).ShouldBe(3 * 10 * 50m + 2 * 10 * 60m);
        series.ShouldContain(p => p.VolumeKg == 0);                 // rest days present, not compressed
    }

    private void AsMember((Guid TenantId, Guid MemberId, Guid ExerciseId, Guid FoodItemId, Guid MemberUserId) ctx)
    {
        CurrentUser.TenantId = ctx.TenantId;
        CurrentUser.UserId = ctx.MemberUserId;
        CurrentUser.IsAuthenticated = true;
    }

    private async Task<(Guid TenantId, Guid MemberId, Guid ExerciseId, Guid FoodItemId, Guid MemberUserId)> SeedAsync(
        bool withDietPlan = false)
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
            FirstName = "Member", LastName = "User"
        };
        db.Users.Add(user);
        db.UserBranchAccesses.Add(new UserBranchAccess { UserId = user.Id, BranchId = branch.Id });

        var member = new Member
        {
            TenantId = tenant.Id, BranchId = branch.Id, UserId = user.Id,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12], FirstName = "Test", LastName = "Member",
            Email = $"{Guid.NewGuid():N}@example.com", JoinDate = new DateOnly(2025, 1, 1),
            QrCodeToken = Guid.NewGuid().ToString("N")
        };
        db.Members.Add(member);

        var exercise = new Exercise { TenantId = tenant.Id, Name = "Deadlift", MuscleGroup = "Back", Equipment = "Barbell" };
        db.Exercises.Add(exercise);

        var food = new FoodItem
        {
            TenantId = tenant.Id, Name = "Chicken Breast", CaloriesPerServing = 165, ProteinG = 31,
            CarbsG = 0, FatG = 3.6m, ServingSizeDescription = "100g"
        };
        db.FoodItems.Add(food);

        if (withDietPlan)
        {
            db.DietPlans.Add(new DietPlan
            {
                TenantId = member.TenantId,
                MemberId = member.Id, Name = "Lean Muscle",
                StartDate = DateOnly.FromDateTime(DateTimeProvider.UtcNow.UtcDateTime).AddDays(-30)
            });
        }

        await db.SaveChangesAsync();
        return (tenant.Id, member.Id, exercise.Id, food.Id, user.Id);
    }
}
