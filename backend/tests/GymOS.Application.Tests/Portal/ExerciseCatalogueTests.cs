using GymOS.Application.Modules.Portal.Queries;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Identity;
using GymOS.Domain.Members;
using GymOS.Domain.Tenancy;
using GymOS.Domain.Workouts;
using GymOS.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Portal;

/// <summary>
/// The picker's read: every movement the gym has, grouped the way a member thinks, carrying their own
/// numbers and nobody else's.
///
/// The rule the whole logging flow rests on is that a member types SETS and WEIGHT and nothing else.
/// That only holds if this query answers everything a picker would otherwise ask for, and answers it
/// truthfully — a pre-filled number that is wrong is worse than an empty field, because the member
/// will accept it.
/// </summary>
public class ExerciseCatalogueTests : ApplicationTestBase
{
    [Fact]
    public async Task Groups_are_canonical_so_two_spellings_of_one_muscle_do_not_become_two_categories()
    {
        var s = await SeedAsync(
            ("Bench Press", "Chest", ExerciseLoadType.Weighted),
            ("Cable Crossover", "Pecs", ExerciseLoadType.Weighted),
            ("Leg Press", "Quads", ExerciseLoadType.Weighted));

        var catalogue = await SendAsync(new GetMyExerciseCatalogueQuery());

        var chest = catalogue.Groups.Single(g => g.Key == "chest");
        chest.Name.ShouldBe("Chest");
        chest.Exercises.Select(e => e.Name).ShouldBe(["Bench Press", "Cable Crossover"]);

        catalogue.Groups.Single(g => g.Key == "legs")
            .Exercises.ShouldHaveSingleItem().Name.ShouldBe("Leg Press");
    }

    [Fact]
    public async Task An_unrecognised_muscle_label_lands_in_Other_rather_than_disappearing()
    {
        // A picker that claims to list everything must list everything. Dropping the row would be the
        // worst outcome: the member would conclude their gym does not have the movement.
        var s = await SeedAsync(("Adductor Machine", "Adductors", ExerciseLoadType.Weighted));

        var catalogue = await SendAsync(new GetMyExerciseCatalogueQuery());

        catalogue.Groups.ShouldHaveSingleItem().Key.ShouldBe("other");
        catalogue.Groups[0].Exercises.ShouldHaveSingleItem().Name.ShouldBe("Adductor Machine");
    }

    [Fact]
    public async Task Groups_come_back_in_catalogue_order_not_alphabetically()
    {
        var s = await SeedAsync(
            ("Treadmill Run", "Cardio", ExerciseLoadType.Distance),
            ("Barbell Squat", "Legs", ExerciseLoadType.Weighted),
            ("Bench Press", "Chest", ExerciseLoadType.Weighted));

        var catalogue = await SendAsync(new GetMyExerciseCatalogueQuery());

        // Chest, Legs, Cardio — the compound groups first and conditioning last, which is the order
        // the tabs are rendered in. Alphabetically this would be Cardio, Chest, Legs.
        catalogue.Groups.Select(g => g.Key).ShouldBe(["chest", "legs", "cardio"]);
    }

    [Fact]
    public async Task Last_sets_sums_the_per_set_rows_of_the_most_recent_session()
    {
        // The logger writes one row per set. Reading a single row's SetsCompleted would offer "1 set"
        // to a member who did four, and they would accept it, and the session would shrink each time.
        var s = await SeedAsync(("Bench Press", "Chest", ExerciseLoadType.Weighted));

        await LogSessionAsync(s, DateTimeProvider.UtcNow.AddDays(-9), (s.Ids["Bench Press"], 1, 10, 50m));
        await LogSessionAsync(s, DateTimeProvider.UtcNow.AddDays(-2),
            (s.Ids["Bench Press"], 1, 8, 60m),
            (s.Ids["Bench Press"], 1, 6, 65m),
            (s.Ids["Bench Press"], 1, 5, 70m));

        var exercise = (await SendAsync(new GetMyExerciseCatalogueQuery()))
            .Groups.SelectMany(g => g.Exercises).ShouldHaveSingleItem();

        exercise.LastSets.ShouldBe(3);
        exercise.LastReps.ShouldBe(5);
        exercise.LastWeightKg.ShouldBe(70m);
        // Two sessions, four rows. Familiarity is measured in visits, not in sets.
        exercise.TimesPerformed.ShouldBe(2);
    }

    [Fact]
    public async Task A_movement_with_no_history_carries_no_numbers_at_all()
    {
        var s = await SeedAsync(("Bench Press", "Chest", ExerciseLoadType.Weighted));

        var exercise = (await SendAsync(new GetMyExerciseCatalogueQuery()))
            .Groups.SelectMany(g => g.Exercises).ShouldHaveSingleItem();

        exercise.LastPerformedAt.ShouldBeNull();
        exercise.LastSets.ShouldBeNull();
        exercise.LastReps.ShouldBeNull();
        exercise.LastWeightKg.ShouldBeNull();
        exercise.TimesPerformed.ShouldBe(0);
    }

    [Fact]
    public async Task An_unloaded_movement_never_reports_a_weight_even_when_one_was_logged()
    {
        // Seeded data really does contain "Treadmill Run, 17.5 kg" — the seeder decided load by
        // string-matching Equipment against "Bodyweight". Filtering it here means no screen built on
        // this query can put kilograms beside a plank, whatever is in the table underneath.
        var s = await SeedAsync(("Plank", "Core", ExerciseLoadType.Timed));
        await LogSessionAsync(s, DateTimeProvider.UtcNow.AddDays(-1), (s.Ids["Plank"], 1, 60, 17.5m));

        var exercise = (await SendAsync(new GetMyExerciseCatalogueQuery()))
            .Groups.SelectMany(g => g.Exercises).ShouldHaveSingleItem();

        exercise.LoadType.ShouldBe("Timed");
        exercise.LastWeightKg.ShouldBeNull();
        // The sets and reps are real and stay — it is only the load that was never a measurement.
        exercise.LastSets.ShouldBe(1);
        exercise.LastReps.ShouldBe(60);
    }

    [Fact]
    public async Task Another_members_history_is_not_shown_as_yours()
    {
        var s = await SeedAsync(("Bench Press", "Chest", ExerciseLoadType.Weighted));
        var stranger = await AddMemberAsync(s);

        await LogSessionAsync(s, DateTimeProvider.UtcNow.AddDays(-1),
            memberId: stranger, rows: [(s.Ids["Bench Press"], 1, 8, 100m)]);

        var exercise = (await SendAsync(new GetMyExerciseCatalogueQuery()))
            .Groups.SelectMany(g => g.Exercises).ShouldHaveSingleItem();

        exercise.LastWeightKg.ShouldBeNull();
        exercise.TimesPerformed.ShouldBe(0);
    }

    // ---- harness ----

    private record Seeded(Guid TenantId, Guid BranchId, Guid MemberId, Dictionary<string, Guid> Ids);

    private async Task<Seeded> SeedAsync(params (string Name, string? MuscleGroup, ExerciseLoadType LoadType)[] exercises)
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
            FirstName = "Lifting",
            LastName = "Member"
        };
        db.Users.Add(user);

        var member = new Member
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            UserId = user.Id,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12],
            FirstName = "Lifting",
            LastName = "Member",
            Email = $"{Guid.NewGuid():N}@example.com",
            JoinDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = MemberStatus.Active,
            QrCodeToken = Guid.NewGuid().ToString("N")
        };
        db.Members.Add(member);

        var ids = new Dictionary<string, Guid>();
        foreach (var (name, muscleGroup, loadType) in exercises)
        {
            var exercise = new Exercise
            {
                TenantId = tenant.Id, Name = name, MuscleGroup = muscleGroup,
                Equipment = "Barbell", LoadType = loadType
            };
            db.Exercises.Add(exercise);
            ids[name] = exercise.Id;
        }

        await db.SaveChangesAsync();

        CurrentUser.TenantId = tenant.Id;
        CurrentUser.UserId = user.Id;
        CurrentUser.IsAuthenticated = true;

        return new Seeded(tenant.Id, branch.Id, member.Id, ids);
    }

    private async Task<Guid> AddMemberAsync(Seeded s)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var member = new Member
        {
            TenantId = s.TenantId,
            BranchId = s.BranchId,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12],
            FirstName = "Someone",
            LastName = "Else",
            Email = $"{Guid.NewGuid():N}@example.com",
            JoinDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = MemberStatus.Active,
            QrCodeToken = Guid.NewGuid().ToString("N")
        };
        db.Members.Add(member);
        await db.SaveChangesAsync();
        return member.Id;
    }

    private async Task LogSessionAsync(
        Seeded s,
        DateTimeOffset loggedAt,
        params (Guid ExerciseId, int Sets, int Reps, decimal? Weight)[] rows)
        => await LogSessionAsync(s, loggedAt, s.MemberId, rows);

    private async Task LogSessionAsync(
        Seeded s,
        DateTimeOffset loggedAt,
        Guid memberId,
        (Guid ExerciseId, int Sets, int Reps, decimal? Weight)[] rows)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var log = new WorkoutLog { TenantId = s.TenantId, MemberId = memberId, LoggedAt = loggedAt };
        foreach (var (exerciseId, sets, reps, weight) in rows)
        {
            log.Entries.Add(new WorkoutLogEntry
            {
                TenantId = s.TenantId,
                ExerciseId = exerciseId,
                SetsCompleted = sets,
                RepsCompleted = reps,
                WeightKg = weight
            });
        }

        db.WorkoutLogs.Add(log);
        await db.SaveChangesAsync();
    }
}
