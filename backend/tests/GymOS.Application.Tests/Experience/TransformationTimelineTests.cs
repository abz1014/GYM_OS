using GymOS.Application.Modules.Experience.Queries;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Experience;
using GymOS.Domain.Identity;
using GymOS.Domain.Members;
using GymOS.Domain.Tenancy;
using GymOS.Domain.Workouts;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Experience;

/// <summary>
/// Slice 7: the transformation timeline merges measurements, photos, achieved goals, personal
/// records, and unlocked achievements into one date-ordered feed — and correctly excludes goals that
/// aren't achieved yet and achievement codes with no catalog match.
/// </summary>
public class TransformationTimelineTests : ApplicationTestBase
{
    [Fact]
    public async Task Timeline_merges_all_five_sources_newest_first()
    {
        var ctx = await SeedAsync();
        SetAuthenticatedAs(ctx.TenantId, ctx.UserId);

        var now = DateTimeProvider.UtcNow;
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

            db.MemberMeasurements.Add(new MemberMeasurement
            {
                MemberId = ctx.MemberId, MeasuredOn = DateOnly.FromDateTime(now.UtcDateTime).AddDays(-10), WeightKg = 82.5m
            });
            db.ProgressPhotos.Add(new ProgressPhoto
            {
                MemberId = ctx.MemberId, PhotoUrl = "https://example.com/photo.jpg", TakenAt = now.AddDays(-8)
            });
            db.MemberGoals.Add(new MemberGoal
            {
                TenantId = ctx.TenantId, MemberId = ctx.MemberId, Title = "Bench 100kg",
                IsAchieved = true, AchievedAt = now.AddDays(-6), CreatedAt = now.AddDays(-30)
            });
            // Not achieved -> must be excluded from the timeline entirely.
            db.MemberGoals.Add(new MemberGoal
            {
                TenantId = ctx.TenantId, MemberId = ctx.MemberId, Title = "Run a 5k",
                IsAchieved = false, CreatedAt = now.AddDays(-30)
            });
            db.PersonalRecords.Add(new PersonalRecord
            {
                TenantId = ctx.TenantId, MemberId = ctx.MemberId, ExerciseId = ctx.ExerciseId,
                Type = PersonalRecordType.MaxWeight, Value = 100m, AchievedAt = now.AddDays(-4)
            });
            db.MemberAchievements.Add(new MemberAchievement
            {
                TenantId = ctx.TenantId, MemberId = ctx.MemberId, Code = "first-workout", UnlockedAt = now.AddDays(-2)
            });

            await db.SaveChangesAsync();
        }

        var timeline = await SendAsync(new GetMyTimelineQuery());

        timeline.Count.ShouldBe(5); // the unachieved goal is excluded
        timeline.ShouldContain(e => e.Type == "Measurement" && e.Description!.Contains("82.5"));
        timeline.ShouldContain(e => e.Type == "Photo" && e.PhotoUrl == "https://example.com/photo.jpg");
        timeline.ShouldContain(e => e.Type == "GoalAchieved" && e.Title.Contains("Bench 100kg"));
        timeline.ShouldContain(e => e.Type == "PersonalRecord" && e.Description!.Contains("100"));
        timeline.ShouldContain(e => e.Type == "Achievement" && e.Title.Contains("First Workout"));
        timeline.ShouldNotContain(e => e.Title.Contains("Run a 5k"));

        // Newest first.
        timeline.Select(e => e.OccurredAt).ShouldBe(timeline.Select(e => e.OccurredAt).OrderByDescending(o => o));
    }

    [Fact]
    public async Task Timeline_is_empty_with_no_history()
    {
        var ctx = await SeedAsync();
        SetAuthenticatedAs(ctx.TenantId, ctx.UserId);

        var timeline = await SendAsync(new GetMyTimelineQuery());

        timeline.ShouldBeEmpty();
    }

    private void SetAuthenticatedAs(Guid tenantId, Guid userId)
    {
        CurrentUser.TenantId = tenantId;
        CurrentUser.UserId = userId;
        CurrentUser.IsAuthenticated = true;
    }

    private async Task<(Guid TenantId, Guid BranchId, Guid MemberId, Guid ExerciseId, Guid UserId)> SeedAsync()
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
            FirstName = "Member",
            LastName = "User"
        };
        db.Users.Add(user);
        db.UserBranchAccesses.Add(new UserBranchAccess { UserId = user.Id, BranchId = branch.Id });

        var member = new Member
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            UserId = user.Id,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12],
            FirstName = "Test",
            LastName = "Member",
            Email = $"{Guid.NewGuid():N}@example.com",
            JoinDate = new DateOnly(2025, 1, 1),
            QrCodeToken = Guid.NewGuid().ToString("N")
        };
        db.Members.Add(member);

        var exercise = new Exercise { TenantId = tenant.Id, Name = "Bench Press", MuscleGroup = "Chest", Equipment = "Barbell" };
        db.Exercises.Add(exercise);

        await db.SaveChangesAsync();
        return (tenant.Id, branch.Id, member.Id, exercise.Id, user.Id);
    }
}
