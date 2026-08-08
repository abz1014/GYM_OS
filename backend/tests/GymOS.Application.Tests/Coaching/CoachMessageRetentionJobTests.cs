using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Identity;
using GymOS.Domain.Members;
using GymOS.Domain.Tenancy;
using GymOS.Domain.Trainers;
using GymOS.Infrastructure.BackgroundJobs;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace GymOS.Application.Tests.Coaching;

/// <summary>
/// The first thing in GymOS that deletes anything on a schedule, so it is tested for what it removes
/// AND for what it leaves — a retention job that is too eager is worse than one that never ran,
/// because the correspondence it took is not coming back.
/// </summary>
public class CoachMessageRetentionJobTests : ApplicationTestBase
{
    private static readonly DateTimeOffset Now = new(2026, 8, 8, 12, 0, 0, TimeSpan.Zero);

    public CoachMessageRetentionJobTests() => DateTimeProvider.UtcNow = Now;

    [Fact]
    public async Task Removes_correspondence_past_the_retention_period()
    {
        var ctx = await SeedAsync();
        await MessageAsync(ctx, Now - CoachMessagePolicy.RetentionPeriod - TimeSpan.FromDays(30), "ancient");

        var removed = await RunJobAsync();

        removed.ShouldBe(1);
        (await CountMessagesAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Leaves_a_conversation_that_is_still_inside_the_period()
    {
        var ctx = await SeedAsync();
        await MessageAsync(ctx, Now - CoachMessagePolicy.RetentionPeriod + TimeSpan.FromDays(30), "recent enough");

        var removed = await RunJobAsync();

        removed.ShouldBe(0);
        (await CountMessagesAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task An_unread_message_is_not_exempt_from_retention()
    {
        var ctx = await SeedAsync();
        // Never opened, and two years old. Keeping it forever because nobody read it would invert
        // the rule — the point is that the text does not live indefinitely, not that it is filed.
        await MessageAsync(ctx, Now - CoachMessagePolicy.RetentionPeriod - TimeSpan.FromDays(1), "unopened", readAt: null);

        await RunJobAsync();

        (await CountMessagesAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Takes_only_the_expired_half_of_a_long_running_conversation()
    {
        var ctx = await SeedAsync();
        await MessageAsync(ctx, Now - CoachMessagePolicy.RetentionPeriod - TimeSpan.FromDays(5), "old");
        await MessageAsync(ctx, Now.AddDays(-3), "current");

        var removed = await RunJobAsync();

        removed.ShouldBe(1);
        var survivor = await SingleMessageBodyAsync();
        survivor.ShouldBe("current");
    }

    [Fact]
    public async Task Running_with_nothing_expired_is_a_no_op()
    {
        await SeedAsync();

        (await RunJobAsync()).ShouldBe(0);
    }

    private async Task<int> RunJobAsync()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var job = new CoachMessageRetentionJob(db, DateTimeProvider, NullLogger<CoachMessageRetentionJob>.Instance);
        return await job.RunAsync();
    }

    private async Task<int> CountMessagesAsync()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        return await db.CoachMessages.IgnoreQueryFilters().CountAsync();
    }

    private async Task<string> SingleMessageBodyAsync()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        return await db.CoachMessages.IgnoreQueryFilters().Select(c => c.Body).SingleAsync();
    }

    private async Task MessageAsync(SeedContext ctx, DateTimeOffset sentAt, string body, DateTimeOffset? readAt = null)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        db.CoachMessages.Add(new CoachMessage
        {
            TenantId = ctx.TenantId, TrainerId = ctx.TrainerId, MemberId = ctx.MemberId,
            Author = CoachMessageAuthor.Member, Body = body, SentAt = sentAt, ReadAt = readAt,
        });
        await db.SaveChangesAsync();
    }

    private async Task<SeedContext> SeedAsync()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);

        var branch = new Branch { TenantId = tenant.Id, Name = "Main", AddressLine = "1 Main St", City = "City", Country = "US" };
        db.Branches.Add(branch);

        var user = new User
        {
            TenantId = tenant.Id, Email = $"{Guid.NewGuid():N}@example.com", PasswordHash = "unused-in-this-test",
            FirstName = "Coach", LastName = "User",
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var trainer = new Trainer { TenantId = tenant.Id, BranchId = branch.Id, UserId = user.Id };
        db.Trainers.Add(trainer);

        var member = new Member
        {
            TenantId = tenant.Id, BranchId = branch.Id,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12], FirstName = "Test", LastName = "Member",
            Email = $"{Guid.NewGuid():N}@example.com", JoinDate = new DateOnly(2024, 1, 1),
        };
        db.Members.Add(member);
        await db.SaveChangesAsync();

        return new SeedContext(tenant.Id, trainer.Id, member.Id);
    }

    private record SeedContext(Guid TenantId, Guid TrainerId, Guid MemberId);
}
