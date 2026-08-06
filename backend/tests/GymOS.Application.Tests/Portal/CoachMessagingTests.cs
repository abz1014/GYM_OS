using GymOS.Application.Common.Exceptions;
using GymOS.Application.Modules.Portal.Commands;
using GymOS.Application.Modules.Portal.Queries;
using GymOS.Application.Modules.Workouts.Commands;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Identity;
using GymOS.Domain.Members;
using GymOS.Domain.Tenancy;
using GymOS.Domain.Trainers;
using GymOS.Domain.Workouts;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using ValidationException = FluentValidation.ValidationException;

namespace GymOS.Application.Tests.Portal;

/// <summary>
/// A member and their trainer, talking.
///
/// The tests that matter here are the boundaries. Free text between people is the one feature where
/// getting "whose data" wrong does more than leak a number — so a member resolves to their own
/// trainer and only their own, cannot address anybody else's, and cannot point their coach at
/// someone else's session.
///
/// Clock fixed to Thursday 2026-08-06.
/// </summary>
public class CoachMessagingTests : ApplicationTestBase
{
    private static readonly DateTimeOffset Thursday = new(2026, 8, 6, 12, 0, 0, TimeSpan.Zero);

    public CoachMessagingTests() => DateTimeProvider.UtcNow = Thursday;

    [Fact]
    public async Task A_member_with_no_trainer_has_nobody_to_write_to()
    {
        var ctx = await SeedAsync();
        AsMember(ctx);

        var coach = await SendAsync(new GetMyCoachQuery());

        coach.TrainerId.ShouldBeNull();
        coach.CanSend.ShouldBeFalse();
        coach.Messages.ShouldBeEmpty();
        await Should.ThrowAsync<ForbiddenAccessException>(() => SendAsync(new MessageMyCoachCommand("Hello?")));
    }

    [Fact]
    public async Task A_member_writes_to_the_trainer_they_are_assigned_to()
    {
        var ctx = await SeedAsync();
        await AssignTrainerAsync(ctx);
        AsMember(ctx);

        await SendAsync(new MessageMyCoachCommand("  Shoulder felt fine today.  "));

        var coach = await SendAsync(new GetMyCoachQuery());
        coach.TrainerId.ShouldBe(ctx.TrainerId);
        coach.CanSend.ShouldBeTrue();
        var message = coach.Messages.ShouldHaveSingleItem();
        message.Author.ShouldBe("Member");
        message.Body.ShouldBe("Shoulder felt fine today.");   // stored trimmed
    }

    [Fact]
    public async Task One_members_conversation_is_invisible_to_another()
    {
        var mine = await SeedAsync();
        var theirs = await SeedAsync();
        await AssignTrainerAsync(mine);
        await AssignTrainerAsync(theirs);

        AsMember(mine);
        await SendAsync(new MessageMyCoachCommand("My knee is sore."));

        AsMember(theirs);
        (await SendAsync(new GetMyCoachQuery())).Messages.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_message_can_point_at_the_members_own_session()
    {
        var ctx = await SeedAsync();
        await AssignTrainerAsync(ctx);
        AsMember(ctx);
        var result = await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(ctx.ExerciseId, 3, 8, 60m)]));

        await SendAsync(new MessageMyCoachCommand("Was this too light?", result.WorkoutLogId));

        var message = (await SendAsync(new GetMyCoachQuery())).Messages.ShouldHaveSingleItem();
        message.WorkoutLogId.ShouldBe(result.WorkoutLogId);
    }

    [Fact]
    public async Task A_message_cannot_point_at_somebody_elses_session()
    {
        var mine = await SeedAsync();
        var theirs = await SeedAsync();
        await AssignTrainerAsync(mine);

        AsMember(theirs);
        var theirSession = await SendAsync(new LogMyWorkoutCommand([new WorkoutLogEntryInput(theirs.ExerciseId, 3, 8, 60m)]));

        AsMember(mine);
        await Should.ThrowAsync<NotFoundException>(
            () => SendAsync(new MessageMyCoachCommand("Look at this.", theirSession.WorkoutLogId)));
    }

    [Fact]
    public async Task When_a_pairing_ends_the_conversation_can_still_be_read_but_not_continued()
    {
        var ctx = await SeedAsync();
        await AssignTrainerAsync(ctx);
        AsMember(ctx);
        await SendAsync(new MessageMyCoachCommand("Thanks for everything."));

        await EndAssignmentAsync(ctx);

        var coach = await SendAsync(new GetMyCoachQuery());
        coach.Messages.Count.ShouldBe(1);          // the history is theirs to keep
        coach.CanSend.ShouldBeFalse();
        await Should.ThrowAsync<ForbiddenAccessException>(() => SendAsync(new MessageMyCoachCommand("One more thing")));
    }

    [Fact]
    public async Task An_empty_message_is_rejected_before_it_reaches_anyone()
    {
        var ctx = await SeedAsync();
        await AssignTrainerAsync(ctx);
        AsMember(ctx);

        await Should.ThrowAsync<ValidationException>(() => SendAsync(new MessageMyCoachCommand("   ")));
    }

    [Fact]
    public async Task Unread_counts_only_what_the_trainer_said()
    {
        var ctx = await SeedAsync();
        await AssignTrainerAsync(ctx);
        AsMember(ctx);
        await SendAsync(new MessageMyCoachCommand("Morning."));
        await TrainerWritesAsync(ctx, "Good session yesterday.");

        (await SendAsync(new GetMyCoachQuery())).UnreadCount.ShouldBe(1);

        await SendAsync(new ReadMyCoachMessagesCommand());

        (await SendAsync(new GetMyCoachQuery())).UnreadCount.ShouldBe(0);
    }

    [Fact]
    public async Task Reading_does_not_clear_the_badge_on_the_trainers_side()
    {
        // A member marking their own messages read would quietly tell the trainer they had been seen.
        var ctx = await SeedAsync();
        await AssignTrainerAsync(ctx);
        AsMember(ctx);
        await SendAsync(new MessageMyCoachCommand("Can we move Thursday?"));

        await SendAsync(new ReadMyCoachMessagesCommand());

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var mine = await db.CoachMessages.SingleAsync(m => m.Author == CoachMessageAuthor.Member);
        mine.ReadAt.ShouldBeNull();
    }

    private void AsMember(SeedContext ctx)
    {
        CurrentUser.TenantId = ctx.TenantId;
        CurrentUser.UserId = ctx.MemberUserId;
        CurrentUser.IsAuthenticated = true;
    }

    private async Task AssignTrainerAsync(SeedContext ctx)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        db.TrainerAssignments.Add(new TrainerAssignment
        {
            TrainerId = ctx.TrainerId,
            MemberId = ctx.MemberId,
            StartDate = DateOnly.FromDateTime(Thursday.UtcDateTime).AddDays(-30),
            IsActive = true
        });
        await db.SaveChangesAsync();
    }

    private async Task EndAssignmentAsync(SeedContext ctx)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var assignment = await db.TrainerAssignments.SingleAsync(a => a.MemberId == ctx.MemberId);
        assignment.EndDate = DateOnly.FromDateTime(Thursday.UtcDateTime).AddDays(-1);
        await db.SaveChangesAsync();
    }

    private async Task TrainerWritesAsync(SeedContext ctx, string body)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        db.CoachMessages.Add(new CoachMessage
        {
            TenantId = ctx.TenantId, TrainerId = ctx.TrainerId, MemberId = ctx.MemberId,
            Author = CoachMessageAuthor.Trainer, Body = body, SentAt = Thursday
        });
        await db.SaveChangesAsync();
    }

    private record SeedContext(Guid TenantId, Guid BranchId, Guid MemberId, Guid MemberUserId, Guid TrainerId, Guid ExerciseId);

    private async Task<SeedContext> SeedAsync()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);

        var branch = new Branch { TenantId = tenant.Id, Name = "Main", AddressLine = "1 Main St", City = "City", Country = "US" };
        db.Branches.Add(branch);

        var memberUser = new User
        {
            TenantId = tenant.Id, Email = $"{Guid.NewGuid():N}@example.com", PasswordHash = "unused-in-this-test",
            FirstName = "Member", LastName = "User"
        };
        db.Users.Add(memberUser);
        db.UserBranchAccesses.Add(new UserBranchAccess { UserId = memberUser.Id, BranchId = branch.Id });

        var trainerUser = new User
        {
            TenantId = tenant.Id, Email = $"{Guid.NewGuid():N}@example.com", PasswordHash = "unused-in-this-test",
            FirstName = "Coach", LastName = "Rivera"
        };
        db.Users.Add(trainerUser);

        var trainer = new Trainer { TenantId = tenant.Id, BranchId = branch.Id, UserId = trainerUser.Id };
        db.Trainers.Add(trainer);

        var member = new Member
        {
            TenantId = tenant.Id, BranchId = branch.Id, UserId = memberUser.Id,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12], FirstName = "Test", LastName = "Member",
            Email = $"{Guid.NewGuid():N}@example.com", JoinDate = new DateOnly(2025, 1, 1),
            QrCodeToken = Guid.NewGuid().ToString("N")
        };
        db.Members.Add(member);

        var exercise = new Exercise { TenantId = tenant.Id, Name = "Bench Press", MuscleGroup = "Chest", Equipment = "Barbell" };
        db.Exercises.Add(exercise);

        await db.SaveChangesAsync();
        return new SeedContext(tenant.Id, branch.Id, member.Id, memberUser.Id, trainer.Id, exercise.Id);
    }
}
