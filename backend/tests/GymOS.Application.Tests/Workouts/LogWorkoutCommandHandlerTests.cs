using GymOS.Application.Common.Exceptions;
using GymOS.Application.Modules.Workouts.Commands;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Members;
using GymOS.Domain.Tenancy;
using GymOS.Domain.Workouts;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Workouts;

/// <summary>
/// LogWorkoutCommand's core business rule: a workout can only be logged against a real member, and
/// every set/rep/weight entry submitted must persist exactly as given — a trainer session's history
/// is only as trustworthy as this write path.
/// </summary>
public class LogWorkoutCommandHandlerTests : ApplicationTestBase
{
    [Fact]
    public async Task Logging_a_workout_persists_the_log_and_all_of_its_entries()
    {
        var (tenantId, memberId, exerciseId) = await SeedAsync();
        CurrentUser.TenantId = tenantId;

        var logId = await SendAsync(new LogWorkoutCommand(
            memberId, null, [new WorkoutLogEntryInput(exerciseId, SetsCompleted: 4, RepsCompleted: 8, WeightKg: 60m)]));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var log = await db.WorkoutLogs.Include(l => l.Entries).SingleAsync(l => l.Id == logId);
        log.MemberId.ShouldBe(memberId);
        log.Entries.ShouldHaveSingleItem();

        var entry = log.Entries.Single();
        entry.ExerciseId.ShouldBe(exerciseId);
        entry.SetsCompleted.ShouldBe(4);
        entry.RepsCompleted.ShouldBe(8);
        entry.WeightKg.ShouldBe(60m);
    }

    [Fact]
    public async Task Logging_a_workout_for_a_nonexistent_member_is_rejected()
    {
        var (tenantId, _, exerciseId) = await SeedAsync();
        CurrentUser.TenantId = tenantId;

        var act = () => SendAsync(new LogWorkoutCommand(
            Guid.NewGuid(), null, [new WorkoutLogEntryInput(exerciseId, 3, 10, null)]));

        await Should.ThrowAsync<NotFoundException>(act);
    }

    private async Task<(Guid TenantId, Guid MemberId, Guid ExerciseId)> SeedAsync()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);

        var branch = new Branch { TenantId = tenant.Id, Name = "Main", AddressLine = "1 Main St", City = "City", Country = "US" };
        db.Branches.Add(branch);

        var member = new Member
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12],
            FirstName = "Test",
            LastName = "Member",
            Email = $"{Guid.NewGuid():N}@example.com",
            JoinDate = new DateOnly(2025, 1, 1),
            QrCodeToken = Guid.NewGuid().ToString("N")
        };
        db.Members.Add(member);

        var exercise = new Exercise { TenantId = tenant.Id, Name = "Barbell Squat", MuscleGroup = "Legs" };
        db.Exercises.Add(exercise);

        await db.SaveChangesAsync();
        return (tenant.Id, member.Id, exercise.Id);
    }
}
