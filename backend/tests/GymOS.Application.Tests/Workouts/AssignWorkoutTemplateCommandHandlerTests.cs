using GymOS.Application.Common.Exceptions;
using GymOS.Application.Modules.Workouts.Commands;
using GymOS.Application.Modules.Workouts.Queries;
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
/// AssignWorkoutTemplateCommand's core business rule: a trainer can put an existing WorkoutTemplate
/// on a specific member's plan (mirroring how CreateDietPlanCommand works for Nutrition), and that
/// assignment must resolve back to the template's actual prescribed exercises — not just an id —
/// since "assign a plan" is only useful to the member if they can see what's actually on it.
/// </summary>
public class AssignWorkoutTemplateCommandHandlerTests : ApplicationTestBase
{
    [Fact]
    public async Task Assigning_a_template_persists_it_with_the_templates_exercises_resolved()
    {
        var ctx = await SeedAsync();
        CurrentUser.TenantId = ctx.TenantId;
        CurrentUser.UserId = ctx.TrainerUserId;

        var assignmentId = await SendAsync(new AssignWorkoutTemplateCommand(
            ctx.MemberId, ctx.TemplateId, new DateOnly(2026, 8, 4), EndDate: null, Notes: "Focus on form"));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var assignment = await db.WorkoutAssignments.SingleAsync(a => a.Id == assignmentId);
        assignment.MemberId.ShouldBe(ctx.MemberId);
        assignment.AssignedByUserId.ShouldBe(ctx.TrainerUserId);
        assignment.Notes.ShouldBe("Focus on form");

        var results = await SendAsync(new GetMemberWorkoutAssignmentsQuery(ctx.MemberId));
        results.ShouldHaveSingleItem();
        var result = results.Single();
        result.WorkoutTemplateName.ShouldBe("Push Day");
        result.Exercises.ShouldHaveSingleItem();
        result.Exercises.Single().ExerciseName.ShouldBe("Bench Press");
        result.Exercises.Single().SetsCount.ShouldBe(4);
    }

    [Fact]
    public async Task Assigning_a_nonexistent_template_is_rejected()
    {
        var ctx = await SeedAsync();
        CurrentUser.TenantId = ctx.TenantId;
        CurrentUser.UserId = ctx.TrainerUserId;

        var act = () => SendAsync(new AssignWorkoutTemplateCommand(
            ctx.MemberId, Guid.NewGuid(), new DateOnly(2026, 8, 4), null, null));

        await Should.ThrowAsync<NotFoundException>(act);
    }

    [Fact]
    public async Task Assigning_to_a_nonexistent_member_is_rejected()
    {
        var ctx = await SeedAsync();
        CurrentUser.TenantId = ctx.TenantId;
        CurrentUser.UserId = ctx.TrainerUserId;

        var act = () => SendAsync(new AssignWorkoutTemplateCommand(
            Guid.NewGuid(), ctx.TemplateId, new DateOnly(2026, 8, 4), null, null));

        await Should.ThrowAsync<NotFoundException>(act);
    }

    private async Task<(Guid TenantId, Guid MemberId, Guid TemplateId, Guid TrainerUserId)> SeedAsync()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);

        var branch = new Branch { TenantId = tenant.Id, Name = "Main", AddressLine = "1 Main St", City = "City", Country = "US" };
        db.Branches.Add(branch);

        var trainerUser = new User
        {
            TenantId = tenant.Id,
            Email = $"{Guid.NewGuid():N}@example.com",
            PasswordHash = "unused-in-this-test",
            FirstName = "Coach",
            LastName = "User"
        };
        db.Users.Add(trainerUser);

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

        var exercise = new Exercise { TenantId = tenant.Id, Name = "Bench Press", MuscleGroup = "Chest" };
        db.Exercises.Add(exercise);

        var template = new WorkoutTemplate { TenantId = tenant.Id, Name = "Push Day", CreatedByUserId = trainerUser.Id };
        db.WorkoutTemplates.Add(template);

        db.WorkoutTemplateExercises.Add(new WorkoutTemplateExercise
        {
            WorkoutTemplateId = template.Id,
            ExerciseId = exercise.Id,
            SetsCount = 4,
            RepsCount = 8,
            OrderIndex = 1
        });

        await db.SaveChangesAsync();
        return (tenant.Id, member.Id, template.Id, trainerUser.Id);
    }
}
