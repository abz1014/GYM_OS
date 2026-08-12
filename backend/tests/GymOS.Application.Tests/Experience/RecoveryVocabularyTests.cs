using GymOS.Application.Modules.Experience.Queries;
using GymOS.Application.Modules.Portal.Queries;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Identity;
using GymOS.Domain.Members;
using GymOS.Domain.Tenancy;
using GymOS.Domain.Workouts;
using GymOS.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Experience;

/// <summary>
/// Recovery and the overload suggestions must agree about what a muscle group is.
///
/// Both read <see cref="Exercise.MuscleGroup"/>, which is free text a gym owner types, and both used
/// it as a key directly. That worked only because DemoDataSeeder writes exactly "Chest"/"Back"/"Legs"
/// — the strings the Train screen's body map also had hard-coded. A gym that types "Quads" produced
/// three separate failures at once: two rows for one leg in the recovery list, a fatigued group the
/// map could not shade, and — the dangerous one — a fatigued group that no longer held back its own
/// exercises, so the screen offered a member a movement it had just told them to rest.
///
/// Resolving on the server through <see cref="MuscleGroupVocabulary"/> makes that class of bug
/// structurally impossible: there is one interpretation, and every consumer gets the same key.
/// </summary>
public class RecoveryVocabularyTests : ApplicationTestBase
{
    [Fact]
    public async Task Two_spellings_of_one_muscle_come_back_as_one_group()
    {
        // A gym that files its leg work under two names a human would call the same thing.
        var s = await SeedAsync(("Barbell Squat", "Quads"), ("Romanian Deadlift", "Hamstrings"));
        await LogAsync(s, daysAgo: 1, "Barbell Squat");
        await LogAsync(s, daysAgo: 2, "Romanian Deadlift");

        var recovery = await SendAsync(new GetMyRecoveryQuery());

        var legs = recovery.MuscleGroups.ShouldHaveSingleItem();
        legs.MuscleGroupKey.ShouldBe("legs");
        legs.MuscleGroup.ShouldBe("Legs");
        // Both sessions counted into the one group, rather than one each into two.
        legs.TimesLast7Days.ShouldBe(2);
    }

    [Fact]
    public async Task An_unrecognised_label_still_gets_a_group_rather_than_vanishing()
    {
        var s = await SeedAsync(("Adductor Machine", "Adductors"));
        await LogAsync(s, daysAgo: 1, "Adductor Machine");

        var recovery = await SendAsync(new GetMyRecoveryQuery());

        recovery.MuscleGroups.ShouldHaveSingleItem().MuscleGroupKey.ShouldBe("other");
    }

    [Fact]
    public async Task The_suggestion_list_keys_its_muscle_group_the_same_way_recovery_does()
    {
        /*
         * The load-bearing one. The Train screen holds back any suggestion whose muscle group is
         * fatigued, and it matches suggestion-key against recovery-key. If the two queries resolved
         * differently — one canonical, one raw — that match would silently never fire and a fatigued
         * leg would keep offering squats.
         */
        var s = await SeedAsync(("Barbell Squat", "Quads"));
        await LogAsync(s, daysAgo: 1, "Barbell Squat");
        await LogAsync(s, daysAgo: 3, "Barbell Squat");

        var recovery = await SendAsync(new GetMyRecoveryQuery());
        var suggestions = await SendAsync(new GetMyWorkoutSuggestionsQuery());

        var suggestion = suggestions.ShouldHaveSingleItem();
        suggestion.MuscleGroupKey.ShouldBe("legs");
        suggestion.MuscleGroup.ShouldBe("Legs");

        // The join the screen performs, asserted directly: the key the suggestion carries must find
        // the group recovery reported.
        recovery.MuscleGroups.Select(m => m.MuscleGroupKey).ShouldContain(suggestion.MuscleGroupKey);
    }

    // ---- harness ----

    private record Seeded(Guid TenantId, Guid MemberId, Dictionary<string, Guid> Ids);

    private async Task<Seeded> SeedAsync(params (string Name, string MuscleGroup)[] exercises)
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
            FirstName = "Leg",
            LastName = "Day"
        };
        db.Users.Add(user);

        var member = new Member
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            UserId = user.Id,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12],
            FirstName = "Leg",
            LastName = "Day",
            Email = $"{Guid.NewGuid():N}@example.com",
            JoinDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = MemberStatus.Active,
            QrCodeToken = Guid.NewGuid().ToString("N")
        };
        db.Members.Add(member);

        var ids = new Dictionary<string, Guid>();
        foreach (var (name, muscleGroup) in exercises)
        {
            var exercise = new Exercise
            {
                TenantId = tenant.Id, Name = name, MuscleGroup = muscleGroup,
                Equipment = "Barbell", LoadType = ExerciseLoadType.Weighted
            };
            db.Exercises.Add(exercise);
            ids[name] = exercise.Id;
        }

        await db.SaveChangesAsync();

        CurrentUser.TenantId = tenant.Id;
        CurrentUser.UserId = user.Id;
        CurrentUser.IsAuthenticated = true;

        return new Seeded(tenant.Id, member.Id, ids);
    }

    private async Task LogAsync(Seeded s, int daysAgo, string exerciseName)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var log = new WorkoutLog
        {
            TenantId = s.TenantId,
            MemberId = s.MemberId,
            LoggedAt = DateTimeProvider.UtcNow.AddDays(-daysAgo)
        };
        log.Entries.Add(new WorkoutLogEntry
        {
            TenantId = s.TenantId,
            ExerciseId = s.Ids[exerciseName],
            SetsCompleted = 3,
            RepsCompleted = 8,
            WeightKg = 60m
        });

        db.WorkoutLogs.Add(log);
        await db.SaveChangesAsync();
    }
}
