using FluentValidation;
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
/// A movement may only be measured the way it is actually measured.
///
/// The defect these exist for: the exercise picker applied a DEFAULT_REPS of 8 to every movement
/// without consulting the load type it already had, the API demanded a rep count, and "8 reps of
/// running" became a stored fact. It then propagated — the next-session proposal re-served it and the
/// picker showed it back as "3 × 8 · 4d ago" — so after one session the fabrication was
/// indistinguishable from something the member had really done.
///
/// The guard REJECTS rather than silently dropping. A caller sending reps for a run has a bug, and
/// quietly nulling the field would leave that bug in place and unfindable — the same judgement the
/// payment ceiling made when accept-and-clamp was rejected in favour of refusing.
/// </summary>
public class LoadTypeLoggingTests : ApplicationTestBase
{
    [Fact]
    public async Task Reps_are_rejected_on_a_movement_measured_in_distance()
    {
        var s = await SeedAsync();

        var reject = await Should.ThrowAsync<ValidationException>(async () =>
            await SendAsync(new LogMyWorkoutCommand(
                [new WorkoutLogEntryInput(s.TreadmillId, 1, RepsCompleted: 8, WeightKg: null)])));

        reject.Message.ShouldContain("Treadmill Run");
        reject.Message.ShouldContain("distance");
    }

    [Fact]
    public async Task Reps_are_rejected_on_a_movement_measured_in_time()
    {
        var s = await SeedAsync();

        await Should.ThrowAsync<ValidationException>(async () =>
            await SendAsync(new LogMyWorkoutCommand(
                [new WorkoutLogEntryInput(s.PlankId, 1, RepsCompleted: 8, WeightKg: null)])));
    }

    [Fact]
    public async Task A_run_is_stored_with_its_distance_and_duration_and_no_reps()
    {
        var s = await SeedAsync();

        await SendAsync(new LogMyWorkoutCommand([
            new WorkoutLogEntryInput(s.TreadmillId, 1, RepsCompleted: null, WeightKg: null,
                DurationSeconds: 1_800, DistanceMeters: 5_000m),
        ]));

        var entry = await SingleEntryAsync(s.MemberId);
        entry.RepsCompleted.ShouldBeNull();
        entry.WeightKg.ShouldBeNull();
        entry.DurationSeconds.ShouldBe(1_800);
        entry.DistanceMeters.ShouldBe(5_000m);
    }

    [Fact]
    public async Task A_plank_is_stored_with_its_duration_alone()
    {
        var s = await SeedAsync();

        await SendAsync(new LogMyWorkoutCommand([
            new WorkoutLogEntryInput(s.PlankId, 3, RepsCompleted: null, WeightKg: null, DurationSeconds: 45),
        ]));

        var entry = await SingleEntryAsync(s.MemberId);
        entry.RepsCompleted.ShouldBeNull();
        entry.DurationSeconds.ShouldBe(45);
        entry.DistanceMeters.ShouldBeNull();
    }

    [Fact]
    public async Task A_lift_still_logs_exactly_as_it_did()
    {
        // The whole change must be invisible to the movements that were already honest.
        var s = await SeedAsync();

        await SendAsync(new LogMyWorkoutCommand([
            new WorkoutLogEntryInput(s.BenchId, 3, RepsCompleted: 8, WeightKg: 60m),
        ]));

        var entry = await SingleEntryAsync(s.MemberId);
        entry.RepsCompleted.ShouldBe(8);
        entry.WeightKg.ShouldBe(60m);
        entry.DurationSeconds.ShouldBeNull();
    }

    [Fact]
    public async Task A_load_is_rejected_on_a_bodyweight_movement_but_allowed_on_a_carry()
    {
        /*
         * Weight is NOT forbidden on a Distance movement, and that is deliberate rather than an
         * oversight: a farmer's carry is measured in distance AND load, and the seeded catalogue
         * contains one. LoadType names the PRIMARY measurement, not the only permissible one. A
         * press-up genuinely has no external load, so that one is refused.
         */
        var s = await SeedAsync();

        await Should.ThrowAsync<ValidationException>(async () =>
            await SendAsync(new LogMyWorkoutCommand(
                [new WorkoutLogEntryInput(s.PushUpId, 3, RepsCompleted: 12, WeightKg: 20m)])));

        await SendAsync(new LogMyWorkoutCommand([
            new WorkoutLogEntryInput(s.CarryId, 1, RepsCompleted: null, WeightKg: 32m,
                DurationSeconds: 60, DistanceMeters: 40m),
        ]));

        var entry = await SingleEntryAsync(s.MemberId);
        entry.WeightKg.ShouldBe(32m);
        entry.DistanceMeters.ShouldBe(40m);
    }

    [Fact]
    public async Task The_next_session_proposal_repeats_a_run_as_a_run()
    {
        // The self-perpetuating half of the defect: a proposal that re-serves a rep count for a run
        // gets it confirmed straight back into the database as the member's own history. And the
        // inverse matters equally — a repeat that FORGETS the distance and duration is a repeat of
        // the movement, not the session, and renders with lift columns the server then rejects.
        var s = await SeedAsync();
        await SendAsync(new LogMyWorkoutCommand([
            new WorkoutLogEntryInput(s.TreadmillId, 1, RepsCompleted: null, WeightKg: null,
                DurationSeconds: 1_800, DistanceMeters: 5_000m),
        ]));

        var proposal = await SendAsync(new GetMyNextSessionQuery());

        var entry = proposal.Entries.ShouldHaveSingleItem();
        entry.Reps.ShouldBeNull();
        entry.LoadType.ShouldBe("Distance");
        entry.DurationSeconds.ShouldBe(1_800);
        entry.DistanceMeters.ShouldBe(5_000m);
    }

    // ---- harness ----

    /// <summary>
    /// The most recently logged entry, ordered CLIENT-SIDE: SQLite cannot ORDER BY a DateTimeOffset,
    /// which is the same constraint every query in this module already reduces in memory for.
    /// </summary>
    private async Task<WorkoutLogEntry> SingleEntryAsync(Guid memberId)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var entries = await db.WorkoutLogEntries.AsNoTracking()
            .Include(e => e.WorkoutLog)
            .Where(e => e.WorkoutLog!.MemberId == memberId)
            .ToListAsync();
        return entries.OrderByDescending(e => e.WorkoutLog!.LoggedAt).First();
    }

    private record Seeded(Guid MemberId, Guid BenchId, Guid PushUpId, Guid PlankId, Guid TreadmillId, Guid CarryId);

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
            FirstName = "Cardio",
            LastName = "Member"
        };
        db.Users.Add(user);

        var member = new Member
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            UserId = user.Id,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12],
            FirstName = "Cardio",
            LastName = "Member",
            Email = $"{Guid.NewGuid():N}@example.com",
            JoinDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = MemberStatus.Active,
            QrCodeToken = Guid.NewGuid().ToString("N")
        };
        db.Members.Add(member);

        Exercise Add(string name, string group, ExerciseLoadType loadType)
        {
            var e = new Exercise { TenantId = tenant.Id, Name = name, MuscleGroup = group, LoadType = loadType };
            db.Exercises.Add(e);
            return e;
        }

        var bench = Add("Bench Press", "Chest", ExerciseLoadType.Weighted);
        var pushUp = Add("Push-Up", "Chest", ExerciseLoadType.Bodyweight);
        var plank = Add("Plank", "Core", ExerciseLoadType.Timed);
        var treadmill = Add("Treadmill Run", "Cardio", ExerciseLoadType.Distance);
        var carry = Add("Farmer's Carry", "Full Body", ExerciseLoadType.Distance);

        await db.SaveChangesAsync();

        CurrentUser.TenantId = tenant.Id;
        CurrentUser.UserId = user.Id;
        CurrentUser.IsAuthenticated = true;

        return new Seeded(member.Id, bench.Id, pushUp.Id, plank.Id, treadmill.Id, carry.Id);
    }
}
