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
/// The gym does not tell you to put more weight on a treadmill.
///
/// ProgressiveOverloadPolicy compares MaxWeightKg and TotalReps and has no other terms, and nothing
/// in the system could tell one movement from another — Exercise carried only free-text MuscleGroup
/// and Equipment. So the member home screen ran a barbell rule over a run, found two identical
/// sessions, called it a plateau and offered 2.5% more weight on a number that meant nothing. The
/// seeder had made it worse by assigning kilograms to anything whose Equipment was not the literal
/// string "Bodyweight", so the database really did hold rows like "Treadmill Run, 4x9, 17.5 kg".
///
/// These pin the fix at the boundary where it belongs. The policy stays a pure rule about a pair of
/// numbers; the query decides which movements those numbers are allowed to describe.
/// </summary>
public class WorkoutSuggestionLoadTypeTests : ApplicationTestBase
{
    [Fact]
    public async Task A_run_logged_identically_twice_produces_no_suggestion()
    {
        var s = await SeedAsync();
        await LogTwoIdenticalSessionsAsync(s.MemberId, s.TenantId, s.TreadmillId);

        var suggestions = await SendAsync(new GetMyWorkoutSuggestionsQuery());

        // Identical sessions are exactly the shape that produces ReadyToIncreaseWeight for a barbell.
        suggestions.ShouldNotContain(x => x.ExerciseName == "Treadmill Run");
    }

    [Fact]
    public async Task A_held_plank_produces_no_suggestion_either()
    {
        // Timed movements fail for a different reason from cardio — reps are meaningless rather than
        // the load being — and both have to be excluded, which one enum value could not express.
        var s = await SeedAsync();
        await LogTwoIdenticalSessionsAsync(s.MemberId, s.TenantId, s.PlankId);

        var suggestions = await SendAsync(new GetMyWorkoutSuggestionsQuery());

        suggestions.ShouldNotContain(x => x.ExerciseName == "Plank");
    }

    [Fact]
    public async Task A_barbell_lift_logged_identically_twice_still_earns_its_heavier_attempt()
    {
        // The other half of the fix: the rule must keep working where it was always right. Without
        // this, "filter everything out" would pass the two tests above.
        var s = await SeedAsync();
        await LogTwoIdenticalSessionsAsync(s.MemberId, s.TenantId, s.BenchId, weightKg: 60m);

        var suggestions = await SendAsync(new GetMyWorkoutSuggestionsQuery());

        var bench = suggestions.ShouldHaveSingleItem();
        bench.ExerciseName.ShouldBe("Bench Press");
        bench.Suggestion.ShouldBe(OverloadSuggestion.ReadyToIncreaseWeight);
        bench.SuggestedNextWeightKg.ShouldBe(61.5m);
    }

    private async Task LogTwoIdenticalSessionsAsync(
        Guid memberId, Guid tenantId, Guid exerciseId, decimal? weightKg = null)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        foreach (var daysAgo in new[] { 7, 1 })
        {
            var log = new WorkoutLog
            {
                TenantId = tenantId,
                MemberId = memberId,
                LoggedAt = DateTimeProvider.UtcNow.AddDays(-daysAgo)
            };
            log.Entries.Add(new WorkoutLogEntry
            {
                TenantId = tenantId,
                ExerciseId = exerciseId,
                SetsCompleted = 3,
                RepsCompleted = 10,
                WeightKg = weightKg
            });
            db.WorkoutLogs.Add(log);
        }

        await db.SaveChangesAsync();
    }

    private async Task<(Guid TenantId, Guid MemberId, Guid TreadmillId, Guid PlankId, Guid BenchId)> SeedAsync()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);

        var branch = new Branch { TenantId = tenant.Id, Name = "Main", AddressLine = "1 Main St", City = "City", Country = "US" };
        db.Branches.Add(branch);

        // A real User row: Member.UserId is a foreign key, so pointing it at a bare Guid fails on
        // save rather than merely failing to resolve.
        var user = new User
        {
            TenantId = tenant.Id,
            Email = $"{Guid.NewGuid():N}@example.com",
            PasswordHash = "unused-in-this-test",
            FirstName = "Running",
            LastName = "Member"
        };
        db.Users.Add(user);

        var member = new Member
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            UserId = user.Id,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12],
            FirstName = "Running",
            LastName = "Member",
            Email = $"{Guid.NewGuid():N}@example.com",
            JoinDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = MemberStatus.Active,
            QrCodeToken = Guid.NewGuid().ToString("N")
        };
        db.Members.Add(member);

        var treadmill = new Exercise
        {
            TenantId = tenant.Id, Name = "Treadmill Run", MuscleGroup = "Cardio",
            Equipment = "Treadmill", LoadType = ExerciseLoadType.Distance
        };
        var plank = new Exercise
        {
            TenantId = tenant.Id, Name = "Plank", MuscleGroup = "Core",
            Equipment = "Bodyweight", LoadType = ExerciseLoadType.Timed
        };
        var bench = new Exercise
        {
            TenantId = tenant.Id, Name = "Bench Press", MuscleGroup = "Chest",
            Equipment = "Barbell", LoadType = ExerciseLoadType.Weighted
        };
        db.Exercises.AddRange(treadmill, plank, bench);

        await db.SaveChangesAsync();

        // The portal resolves the acting member from the JWT's user id, so the caller must BE that user.
        CurrentUser.TenantId = tenant.Id;
        CurrentUser.UserId = user.Id;
        CurrentUser.IsAuthenticated = true;

        return (tenant.Id, member.Id, treadmill.Id, plank.Id, bench.Id);
    }
}
