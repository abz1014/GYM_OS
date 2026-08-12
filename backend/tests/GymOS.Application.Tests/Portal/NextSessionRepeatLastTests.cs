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
/// A session comes back the shape it was performed, not the shape it was written.
///
/// The live logger writes one WorkoutLogEntry per SET — each carrying SetsCompleted = 1 — and the
/// repeat-last proposal replayed those rows one for one. So a member who benched four sets was
/// offered, next time, four separate exercises all called Bench Press, each proposing a single set.
///
/// The distortion compounds: confirm that proposal and it is logged as four exercises, which returns
/// as four again, so the members who used the feature most had the worst proposals. It is also the
/// likeliest explanation for the owner's report that the logging screen "shows only 1 exercise" —
/// the screen renders one movement at a time, and a four-set bench session filled all four slots
/// with the same movement.
///
/// These tests exist because the fix is invisible on the seeded data: the demo member's most recent
/// log happens to contain exactly one entry, so the bug cannot show itself there.
/// </summary>
public class NextSessionRepeatLastTests : ApplicationTestBase
{
    [Fact]
    public async Task Four_sets_of_one_lift_are_proposed_as_one_exercise_with_four_sets()
    {
        var s = await SeedAsync();
        await LogSessionAsync(s, DateTimeProvider.UtcNow.AddDays(-2),
            (s.BenchId, 1, 8, 60m),
            (s.BenchId, 1, 8, 60m),
            (s.BenchId, 1, 6, 65m),
            (s.BenchId, 1, 5, 70m));

        var proposal = await SendAsync(new GetMyNextSessionQuery());

        var entry = proposal.Entries.ShouldHaveSingleItem();
        entry.ExerciseName.ShouldBe("Bench Press");
        entry.Sets.ShouldBe(4);
    }

    [Fact]
    public async Task The_heaviest_set_sets_the_proposed_load_not_the_last_one()
    {
        // A member who works up to a top set and then drops down must be offered the top set again.
        // Taking the LAST row instead would propose the back-off weight and quietly walk them down
        // a little further every session.
        var s = await SeedAsync();
        await LogSessionAsync(s, DateTimeProvider.UtcNow.AddDays(-2),
            (s.BenchId, 1, 5, 80m),
            (s.BenchId, 1, 8, 60m));

        var proposal = await SendAsync(new GetMyNextSessionQuery());

        var entry = proposal.Entries.ShouldHaveSingleItem();
        entry.WeightKg.ShouldBe(80m);
        entry.Reps.ShouldBe(5);
    }

    [Fact]
    public async Task Two_different_lifts_stay_two_exercises()
    {
        // The other half: grouping must not collapse a real two-movement session into one.
        var s = await SeedAsync();
        await LogSessionAsync(s, DateTimeProvider.UtcNow.AddDays(-2),
            (s.BenchId, 1, 8, 60m),
            (s.BenchId, 1, 8, 60m),
            (s.SquatId, 1, 5, 100m));

        var proposal = await SendAsync(new GetMyNextSessionQuery());

        proposal.Entries.Count.ShouldBe(2);
        proposal.Entries.Single(e => e.ExerciseName == "Bench Press").Sets.ShouldBe(2);
        proposal.Entries.Single(e => e.ExerciseName == "Barbell Squat").Sets.ShouldBe(1);
    }

    private async Task LogSessionAsync(
        Seeded s, DateTimeOffset loggedAt, params (Guid ExerciseId, int Sets, int Reps, decimal? Weight)[] rows)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var log = new WorkoutLog { TenantId = s.TenantId, MemberId = s.MemberId, LoggedAt = loggedAt };
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

    private record Seeded(Guid TenantId, Guid MemberId, Guid BenchId, Guid SquatId);

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

        var bench = new Exercise
        {
            TenantId = tenant.Id, Name = "Bench Press", MuscleGroup = "Chest",
            Equipment = "Barbell", LoadType = ExerciseLoadType.Weighted
        };
        var squat = new Exercise
        {
            TenantId = tenant.Id, Name = "Barbell Squat", MuscleGroup = "Legs",
            Equipment = "Barbell", LoadType = ExerciseLoadType.Weighted
        };
        db.Exercises.AddRange(bench, squat);

        await db.SaveChangesAsync();

        CurrentUser.TenantId = tenant.Id;
        CurrentUser.UserId = user.Id;
        CurrentUser.IsAuthenticated = true;

        return new Seeded(tenant.Id, member.Id, bench.Id, squat.Id);
    }
}
