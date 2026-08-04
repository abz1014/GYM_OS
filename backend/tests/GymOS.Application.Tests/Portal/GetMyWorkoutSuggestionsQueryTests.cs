using GymOS.Application.Modules.Portal.Queries;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Identity;
using GymOS.Domain.Members;
using GymOS.Domain.Tenancy;
using GymOS.Domain.Workouts;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Portal;

/// <summary>
/// The suggestion feed must actually be built from the member's own logged history — the same
/// weight/reps across the last two sessions for an exercise must surface as a plateau with a
/// concrete next-weight suggestion, and an exercise logged only once must not.
/// </summary>
public class GetMyWorkoutSuggestionsQueryTests : ApplicationTestBase
{
    [Fact]
    public async Task A_plateaued_exercise_surfaces_a_concrete_weight_suggestion()
    {
        var (tenantId, userId, exerciseId) = await SeedAsync();
        CurrentUser.TenantId = tenantId;
        CurrentUser.UserId = userId;
        CurrentUser.IsAuthenticated = true;

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            var member = await db.Members.SingleAsync(m => m.UserId == userId);

            var older = new WorkoutLog { MemberId = member.Id, LoggedAt = DateTimeProvider.UtcNow.AddDays(-7) };
            older.Entries.Add(new WorkoutLogEntry { ExerciseId = exerciseId, SetsCompleted = 3, RepsCompleted = 10, WeightKg = 60m });
            db.WorkoutLogs.Add(older);

            var newer = new WorkoutLog { MemberId = member.Id, LoggedAt = DateTimeProvider.UtcNow };
            newer.Entries.Add(new WorkoutLogEntry { ExerciseId = exerciseId, SetsCompleted = 3, RepsCompleted = 10, WeightKg = 60m });
            db.WorkoutLogs.Add(newer);

            await db.SaveChangesAsync();
        }

        var suggestions = await SendAsync(new GetMyWorkoutSuggestionsQuery());

        suggestions.ShouldHaveSingleItem();
        suggestions[0].ExerciseId.ShouldBe(exerciseId);
        suggestions[0].Suggestion.ShouldBe(OverloadSuggestion.ReadyToIncreaseWeight);
        suggestions[0].LastWeightKg.ShouldBe(60m);
        suggestions[0].SuggestedNextWeightKg.ShouldBe(ProgressiveOverloadPolicy.SuggestedNextWeightKg(60m));
    }

    [Fact]
    public async Task An_exercise_logged_only_once_reports_insufficient_data_with_no_suggested_weight()
    {
        var (tenantId, userId, exerciseId) = await SeedAsync();
        CurrentUser.TenantId = tenantId;
        CurrentUser.UserId = userId;
        CurrentUser.IsAuthenticated = true;

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
            var member = await db.Members.SingleAsync(m => m.UserId == userId);

            var log = new WorkoutLog { MemberId = member.Id, LoggedAt = DateTimeProvider.UtcNow };
            log.Entries.Add(new WorkoutLogEntry { ExerciseId = exerciseId, SetsCompleted = 3, RepsCompleted = 8, WeightKg = 40m });
            db.WorkoutLogs.Add(log);

            await db.SaveChangesAsync();
        }

        var suggestions = await SendAsync(new GetMyWorkoutSuggestionsQuery());

        suggestions.ShouldHaveSingleItem();
        suggestions[0].Suggestion.ShouldBe(OverloadSuggestion.InsufficientData);
        suggestions[0].SuggestedNextWeightKg.ShouldBeNull();
    }

    private async Task<(Guid TenantId, Guid UserId, Guid ExerciseId)> SeedAsync()
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
            FirstName = "Portal",
            LastName = "Member"
        };
        db.Users.Add(user);

        var member = new Member
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            UserId = user.Id,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12],
            FirstName = "Portal",
            LastName = "Member",
            Email = $"{Guid.NewGuid():N}@example.com",
            JoinDate = new DateOnly(2025, 1, 1),
            QrCodeToken = Guid.NewGuid().ToString("N")
        };
        db.Members.Add(member);

        var exercise = new Exercise { TenantId = tenant.Id, Name = "Bench Press", MuscleGroup = "Chest" };
        db.Exercises.Add(exercise);

        await db.SaveChangesAsync();
        return (tenant.Id, user.Id, exercise.Id);
    }
}
