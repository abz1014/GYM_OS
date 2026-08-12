using GymOS.Application.Modules.Experience.Queries;
using GymOS.Application.Modules.Portal.Commands;
using GymOS.Application.Modules.Portal.Queries;
using GymOS.Application.Modules.Workouts.Commands;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Identity;
using GymOS.Domain.Members;
using GymOS.Domain.Tenancy;
using GymOS.Domain.Workouts;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Workouts;

/// <summary>
/// A movement works more than one muscle, and the app has to know which claims that licenses.
///
/// THE DEFECT: Exercise.MuscleGroup is one label, so a deadlift was "Back" and nothing else. The
/// morning after heavy deadlifts the recovery list told the member their legs were "fully rested — a
/// good target for your next session". Worse than a wrong number, because acting on it means loading
/// a fatigued muscle.
///
/// THE LINE, and every test here defends one side of it:
///
///   PRIMARY decides where a movement is FILED — picker, passport, session name, mastery. Each needs
///   a movement in exactly one place, and each is arithmetic a member can check.
///
///   PRIMARY + SECONDARY decide what a session WORKED — recovery, and the body map drawn from it.
///   These claim something about the member's body, and there the honest answer is that a deadlift
///   worked your back.
///
/// The consequence worth protecting: NO stored member number moves. The tests below assert that
/// explicitly, because the tempting version of this feature — count secondary work everywhere —
/// would inflate every mastery bar overnight without anybody training more.
/// </summary>
public class MultiMuscleTests : ApplicationTestBase
{
    [Fact]
    public async Task A_deadlift_leaves_the_legs_needing_recovery_not_rested()
    {
        // The sentence this whole change exists for.
        var s = await SeedAsync();
        await SendAsync(new LogMyWorkoutCommand([
            new WorkoutLogEntryInput(s.DeadliftId, 3, RepsCompleted: 5, WeightKg: 140m),
        ]));

        var recovery = await SendAsync(new GetMyRecoveryQuery());
        var legs = recovery.MuscleGroups.Single(g => g.MuscleGroupKey == "legs");

        legs.Status.ShouldBe("Fatigued");
        legs.Reason.ShouldContain("another movement");
        // Worked yesterday, but by a movement filed under Back — the screen has to say which.
        legs.TrainedDirectly.ShouldBeFalse();
    }

    [Fact]
    public async Task The_group_the_movement_is_for_still_reads_as_trained_directly()
    {
        var s = await SeedAsync();
        await SendAsync(new LogMyWorkoutCommand([
            new WorkoutLogEntryInput(s.DeadliftId, 3, RepsCompleted: 5, WeightKg: 140m),
        ]));

        var back = (await SendAsync(new GetMyRecoveryQuery())).MuscleGroups
            .Single(g => g.MuscleGroupKey == "back");

        back.Status.ShouldBe("Fatigued");
        back.TrainedDirectly.ShouldBeTrue();
        back.Reason.ShouldContain("Trained in the last day");
    }

    [Fact]
    public async Task Two_movements_working_one_group_on_one_day_count_that_day_once()
    {
        /*
         * "Times in the last 7 days" has always meant DAYS TRAINED, not movements performed, and
         * fanning one exercise out to several groups is exactly the change that could have broken
         * it. A squat (legs primary, core secondary) plus a deadlift (back primary, legs secondary)
         * in one session must leave legs on one day, not two — otherwise a single hard session walks
         * a member toward the 4×-a-week overtraining warning by itself.
         */
        var s = await SeedAsync();
        await SendAsync(new LogMyWorkoutCommand([
            new WorkoutLogEntryInput(s.SquatId, 3, RepsCompleted: 5, WeightKg: 100m),
            new WorkoutLogEntryInput(s.DeadliftId, 3, RepsCompleted: 5, WeightKg: 140m),
        ]));

        var legs = (await SendAsync(new GetMyRecoveryQuery())).MuscleGroups
            .Single(g => g.MuscleGroupKey == "legs");

        legs.TimesLast7Days.ShouldBe(1);
        // That same day also held a squat, whose PRIMARY is legs — so the most recent work on the
        // group WAS direct, and the reason must not call it incidental.
        legs.TrainedDirectly.ShouldBeTrue();
    }

    [Fact]
    public async Task A_run_reaches_the_legs_where_it_used_to_reach_nothing_the_map_could_draw()
    {
        // Cardio has no silhouette zone, so a member whose only training was running saw an entirely
        // untouched body. Their legs did the work; now the map can say so.
        var s = await SeedAsync();
        await SendAsync(new LogMyWorkoutCommand([
            new WorkoutLogEntryInput(s.TreadmillId, 1, RepsCompleted: null, WeightKg: null,
                DurationSeconds: 1_800, DistanceMeters: 5_000m),
        ]));

        var recovery = await SendAsync(new GetMyRecoveryQuery());

        recovery.MuscleGroups.Select(g => g.MuscleGroupKey).ShouldContain("legs");
        recovery.MuscleGroups.Select(g => g.MuscleGroupKey).ShouldContain("cardio");
    }

    [Fact]
    public async Task Muscle_group_mastery_counts_the_movement_once_and_only_where_it_is_filed()
    {
        /*
         * The number this change deliberately does NOT move, and the reason the whole design has a
         * line in it.
         *
         * Mastery sums sessions and volume per group. If a deadlift counted for legs as well as
         * back, its full volume would land in both — and because MasteryPolicy is a bounded score,
         * a member's leg mastery would climb overnight without them training their legs once more
         * than yesterday. Any fraction we might split it by would be invented; the app holds no
         * intensity model. So mastery reads the primary label, and only the primary label.
         */
        var s = await SeedAsync();
        await SendAsync(new LogMyWorkoutCommand([
            new WorkoutLogEntryInput(s.DeadliftId, 3, RepsCompleted: 5, WeightKg: 140m),
        ]));

        var mastery = await SendAsync(new GetMyMasteryQuery());

        mastery.MuscleGroups.ShouldHaveSingleItem().Name.ShouldBe("Back");
    }

    [Fact]
    public async Task The_passport_files_a_movement_in_exactly_one_region()
    {
        /*
         * Coverage is arithmetic the member can check: the headline says "n of N movements" and the
         * regions below it are supposed to add up to that. A deadlift appearing under both Legs and
         * Back would make the sum of the regions exceed the catalogue it claims to cover.
         */
        await SeedAsync();

        var passport = await SendAsync(new GetMyPassportQuery());

        passport.Stamps.Sum(r => r.Available).ShouldBe(passport.Available);
        passport.Stamps.SelectMany(r => r.Entries).Select(e => e.ExerciseId).ShouldBeUnique();
    }

    [Fact]
    public async Task A_gyms_own_new_movement_gets_a_primary_and_no_invented_anatomy()
    {
        // We know what a deadlift works because somebody wrote it down. Nobody here knows what this
        // gym's "Sled Push" works, and inferring it from the name would be the app inventing anatomy.
        await SeedAsync();

        var id = await SendAsync(new CreateExerciseCommand("Sled Push", "Quads", "Sled", null, null));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var muscles = await db.ExerciseMuscles.AsNoTracking().Where(m => m.ExerciseId == id).ToListAsync();

        var only = muscles.ShouldHaveSingleItem();
        only.Role.ShouldBe(MuscleRole.Primary);
        // Resolved through the vocabulary, so the gym's "Quads" files under the same Legs the body
        // map can actually shade.
        only.MuscleGroupKey.ShouldBe("legs");
    }

    [Fact]
    public async Task A_movement_with_no_muscle_rows_still_reports_its_own_group()
    {
        /*
         * The degradation path. A catalogue that predates this table — or one restored from a
         * backup taken before the migration — must keep working: recovery falls back to
         * Exercise.MuscleGroup and behaves exactly as it did before, rather than going silent and
         * showing a member an empty body.
         */
        var s = await SeedAsync();

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            var rows = await db.ExerciseMuscles.Where(m => m.ExerciseId == s.DeadliftId).ToListAsync();
            db.ExerciseMuscles.RemoveRange(rows);
            await db.SaveChangesAsync();
        }

        await SendAsync(new LogMyWorkoutCommand([
            new WorkoutLogEntryInput(s.DeadliftId, 3, RepsCompleted: 5, WeightKg: 140m),
        ]));

        var recovery = await SendAsync(new GetMyRecoveryQuery());

        var back = recovery.MuscleGroups.ShouldHaveSingleItem();
        back.MuscleGroupKey.ShouldBe("back");
        back.TrainedDirectly.ShouldBeTrue();
    }

    // ---- harness ----

    private record Seeded(Guid MemberId, Guid DeadliftId, Guid SquatId, Guid TreadmillId);

    private async Task<Seeded> SeedAsync()
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
            FirstName = "Multi",
            LastName = "Muscle"
        };
        db.Users.Add(user);

        var member = new Member
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            UserId = user.Id,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12],
            FirstName = "Multi",
            LastName = "Muscle",
            Email = $"{Guid.NewGuid():N}@example.com",
            JoinDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = MemberStatus.Active,
            QrCodeToken = Guid.NewGuid().ToString("N")
        };
        db.Members.Add(member);

        Exercise Add(string name, string group, ExerciseLoadType loadType, params (string Key, MuscleRole Role)[] muscles)
        {
            var e = new Exercise { TenantId = tenant.Id, Name = name, MuscleGroup = group, LoadType = loadType };
            foreach (var (key, role) in muscles)
            {
                e.Muscles.Add(new ExerciseMuscle { TenantId = tenant.Id, MuscleGroupKey = key, Role = role });
            }
            db.Exercises.Add(e);
            return e;
        }

        var deadlift = Add("Deadlift", "Back", ExerciseLoadType.Weighted,
            ("back", MuscleRole.Primary), ("legs", MuscleRole.Secondary), ("core", MuscleRole.Secondary));
        var squat = Add("Barbell Squat", "Legs", ExerciseLoadType.Weighted,
            ("legs", MuscleRole.Primary), ("core", MuscleRole.Secondary), ("back", MuscleRole.Secondary));
        var treadmill = Add("Treadmill Run", "Cardio", ExerciseLoadType.Distance,
            ("cardio", MuscleRole.Primary), ("legs", MuscleRole.Secondary));

        await db.SaveChangesAsync();

        CurrentUser.TenantId = tenant.Id;
        CurrentUser.UserId = user.Id;
        CurrentUser.IsAuthenticated = true;

        return new Seeded(member.Id, deadlift.Id, squat.Id, treadmill.Id);
    }
}
