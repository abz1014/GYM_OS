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
/// A member's first ever session is a push, a pull and a squat — not the first three movements in the
/// catalogue when sorted by name.
///
/// This was fine by accident: with fifteen seeded exercises, alphabetical order began Barbell Squat,
/// Bench Press, Bent-Over Row, which is exactly the right answer. Against the full sixty-five-movement
/// catalogue the same code opens a beginner's first workout with Ab Wheel Rollout, Arnold Press and
/// Barbell Curl — three isolation movements that share no purpose and train neither legs nor back.
///
/// The moment a piece of behaviour is correct only because of the size of the data behind it, it is
/// worth a test that says so out loud.
/// </summary>
public class StarterSessionTests : ApplicationTestBase
{
    [Fact]
    public async Task A_first_session_is_the_named_starter_movements_in_order()
    {
        await SeedAsync(
            "Ab Wheel Rollout", "Arnold Press", "Barbell Curl",
            "Bench Press", "Bent-Over Row", "Barbell Squat");

        var proposal = await SendAsync(new GetMyNextSessionQuery());

        proposal.Source.ShouldBe(nameof(SessionProposalSource.Starter));
        proposal.Entries.Select(e => e.ExerciseName)
            .ShouldBe(["Barbell Squat", "Bench Press", "Bent-Over Row"]);
    }

    [Fact]
    public async Task A_gym_with_none_of_the_named_movements_still_gets_a_session()
    {
        // The names are matched, not required. A gym whose catalogue is entirely its own must not end
        // up with an empty proposal and a member told there is nothing to start.
        await SeedAsync("Sled Push", "Battle Ropes", "Sandbag Carry", "Wall Ball");

        var proposal = await SendAsync(new GetMyNextSessionQuery());

        proposal.Source.ShouldBe(nameof(SessionProposalSource.Starter));
        proposal.Entries.Count.ShouldBe(SessionProposalPolicy.StarterExerciseCount);
    }

    private async Task SeedAsync(params string[] exerciseNames)
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
            FirstName = "New",
            LastName = "Member"
        };
        db.Users.Add(user);

        db.Members.Add(new Member
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            UserId = user.Id,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12],
            FirstName = "New",
            LastName = "Member",
            Email = $"{Guid.NewGuid():N}@example.com",
            JoinDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = MemberStatus.Active,
            QrCodeToken = Guid.NewGuid().ToString("N")
        });

        foreach (var name in exerciseNames)
        {
            db.Exercises.Add(new Exercise
            {
                TenantId = tenant.Id, Name = name, MuscleGroup = "Full Body",
                Equipment = "Barbell", LoadType = ExerciseLoadType.Weighted
            });
        }

        await db.SaveChangesAsync();

        CurrentUser.TenantId = tenant.Id;
        CurrentUser.UserId = user.Id;
        CurrentUser.IsAuthenticated = true;
    }
}
