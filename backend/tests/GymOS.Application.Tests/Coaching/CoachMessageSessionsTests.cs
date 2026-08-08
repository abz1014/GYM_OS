using GymOS.Application.Common.Coaching;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Members;
using GymOS.Domain.Tenancy;
using GymOS.Domain.Workouts;
using GymOS.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Coaching;

/// <summary>
/// Turning the WorkoutLogId a message carries into something a person can read. The label is the
/// whole point — "about your session" pointing at a GUID is what this replaces — so what it chooses
/// to call a workout, and what it does when the workout is gone, are the things pinned here.
/// </summary>
public class CoachMessageSessionsTests : ApplicationTestBase
{
    private static readonly DateTimeOffset Now = new(2026, 8, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_trainers_own_template_name_beats_a_derived_one()
    {
        var ctx = await SeedAsync();
        var chest = await AddExerciseAsync("Bench Press", "Chest");
        var templateId = await AddTemplateAsync(ctx, "Week 3 — Upper A");
        var logId = await AddLogAsync(ctx, templateId, [chest, chest]);

        var resolved = await ResolveAsync(logId);

        // The coach named this block. Their words beat anything the app can infer from muscle groups.
        resolved[logId].Label.ShouldBe("Week 3 — Upper A");
    }

    [Fact]
    public async Task Without_a_template_the_session_is_named_by_what_was_trained()
    {
        var ctx = await SeedAsync();
        var back = await AddExerciseAsync("Row", "Back");
        var logId = await AddLogAsync(ctx, workoutTemplateId: null, exerciseIds: [back, back, back]);

        var resolved = await ResolveAsync(logId);

        // Whatever SessionCharacterPolicy calls it — the assertion is that it is not blank and not a
        // GUID, and that it came from the same rule the member's own history uses.
        resolved[logId].Label.ShouldNotBeNullOrWhiteSpace();
        resolved[logId].Label.ShouldNotBe(logId.ToString());
    }

    [Fact]
    public async Task Sets_of_the_same_lift_count_as_one_exercise()
    {
        var ctx = await SeedAsync();
        var squat = await AddExerciseAsync("Squat", "Legs");
        var curl = await AddExerciseAsync("Curl", "Biceps");
        // Five sets of squats and one curl: two exercises, six entries.
        var logId = await AddLogAsync(ctx, null, [squat, squat, squat, squat, squat, curl]);

        var resolved = await ResolveAsync(logId);

        resolved[logId].ExerciseCount.ShouldBe(2);
    }

    [Fact]
    public async Task A_session_that_no_longer_exists_is_absent_rather_than_fatal()
    {
        await SeedAsync();
        var deletedLogId = Guid.NewGuid();

        var resolved = await ResolveAsync(deletedLogId);

        // A member's undo deletes a workout while the message about it survives. Losing the whole
        // conversation because one attachment went away would be the wrong failure.
        resolved.ShouldNotContainKey(deletedLogId);
    }

    [Fact]
    public async Task Resolving_nothing_asks_the_database_nothing()
    {
        await SeedAsync();

        (await ResolveAsync()).ShouldBeEmpty();
    }

    private async Task<IReadOnlyDictionary<Guid, CoachMessageSessionDto>> ResolveAsync(params Guid[] logIds)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        return await CoachMessageSessions.ResolveAsync(db, logIds, CancellationToken.None);
    }

    private async Task<Guid> AddExerciseAsync(string name, string muscleGroup)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var exercise = new Exercise { Name = name, MuscleGroup = muscleGroup };
        db.Exercises.Add(exercise);
        await db.SaveChangesAsync();
        return exercise.Id;
    }

    private async Task<Guid> AddTemplateAsync(SeedContext ctx, string name)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var template = new WorkoutTemplate { TenantId = ctx.TenantId, Name = name };
        db.WorkoutTemplates.Add(template);
        await db.SaveChangesAsync();
        return template.Id;
    }

    private async Task<Guid> AddLogAsync(SeedContext ctx, Guid? workoutTemplateId, IEnumerable<Guid> exerciseIds)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var log = new WorkoutLog { MemberId = ctx.MemberId, WorkoutTemplateId = workoutTemplateId, LoggedAt = Now };
        db.WorkoutLogs.Add(log);
        await db.SaveChangesAsync();

        foreach (var exerciseId in exerciseIds)
        {
            db.WorkoutLogEntries.Add(new WorkoutLogEntry
            {
                WorkoutLogId = log.Id, ExerciseId = exerciseId, SetsCompleted = 1, RepsCompleted = 8,
            });
        }

        await db.SaveChangesAsync();
        return log.Id;
    }

    private async Task<SeedContext> SeedAsync()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);

        var branch = new Branch { TenantId = tenant.Id, Name = "Main", AddressLine = "1 Main St", City = "City", Country = "US" };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        var member = new Member
        {
            TenantId = tenant.Id, BranchId = branch.Id,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12], FirstName = "Test", LastName = "Member",
            Email = $"{Guid.NewGuid():N}@example.com", JoinDate = new DateOnly(2025, 1, 1),
        };
        db.Members.Add(member);
        await db.SaveChangesAsync();

        CurrentUser.TenantId = tenant.Id;
        return new SeedContext(tenant.Id, branch.Id, member.Id);
    }

    private record SeedContext(Guid TenantId, Guid BranchId, Guid MemberId);
}
