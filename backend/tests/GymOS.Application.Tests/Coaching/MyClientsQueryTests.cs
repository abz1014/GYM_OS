using GymOS.Application.Common.Exceptions;
using GymOS.Application.Modules.Coaching.Commands;
using GymOS.Application.Modules.Coaching.Queries;
using GymOS.Application.Tests.TestSupport;
using GymOS.Domain.Identity;
using GymOS.Domain.Members;
using GymOS.Domain.Notifications;
using GymOS.Domain.Tenancy;
using GymOS.Domain.Trainers;
using GymOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace GymOS.Application.Tests.Coaching;

/// <summary>
/// The trainer's roster. Two things matter here and neither is the list itself: that a trainer sees
/// only their own clients, and that the unread count means "this person is waiting on me" rather
/// than "there are unread messages somewhere in this thread".
/// </summary>
public class MyClientsQueryTests : ApplicationTestBase
{
    private static readonly DateTimeOffset Now = new(2026, 8, 8, 10, 0, 0, TimeSpan.Zero);

    public MyClientsQueryTests() => DateTimeProvider.UtcNow = Now;

    [Fact]
    public async Task A_trainer_sees_only_their_own_clients()
    {
        var ctx = await SeedAsync();
        var mine = await AddMemberAsync(ctx, "Mine");
        var theirs = await AddMemberAsync(ctx, "Theirs");
        var otherTrainer = await AddTrainerAsync(ctx, "Other");

        await AssignAsync(ctx.TrainerId, mine);
        await AssignAsync(otherTrainer, theirs);
        AsTrainer(ctx);

        var clients = await SendAsync(new GetMyClientsQuery());

        clients.Select(c => c.MemberId).ShouldBe([mine]);
    }

    [Fact]
    public async Task An_account_that_is_not_a_trainer_is_refused_rather_than_shown_an_empty_list()
    {
        var ctx = await SeedAsync();
        // An owner or manager: real staff, real permission, no Trainer row. An empty roster would
        // read as "you have no clients" when the truth is "you are not a coach".
        CurrentUser.TenantId = ctx.TenantId;
        CurrentUser.UserId = ctx.NonTrainerUserId;
        CurrentUser.IsAuthenticated = true;

        await Should.ThrowAsync<ForbiddenAccessException>(() => SendAsync(new GetMyClientsQuery()));
    }

    [Fact]
    public async Task Only_the_members_own_messages_count_as_unread()
    {
        var ctx = await SeedAsync();
        var memberId = await AddMemberAsync(ctx, "Talkative");
        await AssignAsync(ctx.TrainerId, memberId);

        await MessageAsync(ctx, memberId, CoachMessageAuthor.Member, "Should I add weight?", readAt: null);
        // The trainer's own unread message means the MEMBER hasn't opened it. Counting it here would
        // light up the trainer's inbox for something they wrote themselves.
        await MessageAsync(ctx, memberId, CoachMessageAuthor.Trainer, "Yes — go to 65.", readAt: null);
        AsTrainer(ctx);

        var client = (await SendAsync(new GetMyClientsQuery())).Single();

        client.UnreadFromMember.ShouldBe(1);
    }

    [Fact]
    public async Task Clients_waiting_on_a_reply_come_first()
    {
        var ctx = await SeedAsync();
        var quiet = await AddMemberAsync(ctx, "Quiet");
        var waiting = await AddMemberAsync(ctx, "Waiting");
        await AssignAsync(ctx.TrainerId, quiet);
        await AssignAsync(ctx.TrainerId, waiting);

        // The quiet one messaged more recently, but has already been answered and read.
        await MessageAsync(ctx, waiting, CoachMessageAuthor.Member, "Still sore from Tuesday", readAt: null, sentAt: Now.AddHours(-5));
        await MessageAsync(ctx, quiet, CoachMessageAuthor.Member, "Thanks!", readAt: Now.AddHours(-1), sentAt: Now.AddHours(-2));
        AsTrainer(ctx);

        var clients = await SendAsync(new GetMyClientsQuery());

        // Recency loses to "is someone waiting on me", which is the question the screen exists for.
        clients[0].MemberId.ShouldBe(waiting);
        clients[1].MemberId.ShouldBe(quiet);
    }

    [Fact]
    public async Task An_ended_pairing_stays_on_the_roster_marked_inactive()
    {
        var ctx = await SeedAsync();
        var former = await AddMemberAsync(ctx, "Former");
        await AssignAsync(ctx.TrainerId, former, isActive: false);
        AsTrainer(ctx);

        var client = (await SendAsync(new GetMyClientsQuery())).Single();

        // Readable history, no new messages — the send block is CoachMessagePolicy's job, not this
        // query's, so the row is present and honest about its state rather than hidden.
        client.IsActivePairing.ShouldBeFalse();
    }

    [Fact]
    public async Task A_client_re_signed_after_a_break_appears_once()
    {
        var ctx = await SeedAsync();
        var memberId = await AddMemberAsync(ctx, "Returner");
        await AssignAsync(ctx.TrainerId, memberId, isActive: false, startDate: new DateOnly(2025, 1, 1));
        await AssignAsync(ctx.TrainerId, memberId, isActive: true, startDate: new DateOnly(2026, 6, 1));
        AsTrainer(ctx);

        var clients = await SendAsync(new GetMyClientsQuery());

        // Two assignment rows, one person, one conversation — and the latest assignment decides.
        clients.Count.ShouldBe(1);
        clients[0].IsActivePairing.ShouldBeTrue();
    }

    [Fact]
    public async Task Replying_schedules_a_notification_so_the_member_learns_of_it()
    {
        var ctx = await SeedAsync();
        var memberId = await AddMemberAsync(ctx, "Reachable");
        await AssignAsync(ctx.TrainerId, memberId);
        await AddCoachReplyTemplateAsync(ctx);
        AsTrainer(ctx);

        await SendAsync(new MessageMyClientCommand(memberId, "Nice work on Tuesday."));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var scheduled = await db.ScheduledNotifications.IgnoreQueryFilters()
            .SingleAsync(n => n.RecipientMemberId == memberId);

        scheduled.Status.ShouldBe(ScheduledNotificationStatus.Pending);
        scheduled.RelatedEntityType.ShouldBe(nameof(CoachMessage));
    }

    [Fact]
    public async Task A_missing_template_does_not_cost_the_member_the_message()
    {
        var ctx = await SeedAsync();
        var memberId = await AddMemberAsync(ctx, "Reachable");
        await AssignAsync(ctx.TrainerId, memberId);
        // No coach-reply template seeded — a gym that deleted it, or an older tenant.
        AsTrainer(ctx);

        await SendAsync(new MessageMyClientCommand(memberId, "Nice work on Tuesday."));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        // The notification is a courtesy; the message is the thing the trainer wrote. Losing the
        // second because the first could not be arranged would be the wrong trade every time.
        (await db.CoachMessages.IgnoreQueryFilters().CountAsync(c => c.MemberId == memberId)).ShouldBe(1);
        (await db.ScheduledNotifications.IgnoreQueryFilters().AnyAsync(n => n.RecipientMemberId == memberId)).ShouldBeFalse();
    }

    [Fact]
    public async Task Filling_the_hourly_allowance_stops_further_sends_into_that_pairing()
    {
        var ctx = await SeedAsync();
        var memberId = await AddMemberAsync(ctx, "Chatty");
        await AssignAsync(ctx.TrainerId, memberId);

        // The allowance already spent this hour, by this trainer, into this pairing.
        for (var i = 0; i < CoachMessagePolicy.MaxMessagesPerHour; i++)
        {
            await MessageAsync(ctx, memberId, CoachMessageAuthor.Trainer, $"msg {i}", readAt: null, sentAt: Now.AddMinutes(-i - 1));
        }

        AsTrainer(ctx);

        // 429, not 400 — this one comes back on its own, and a client retrying later is correct.
        await Should.ThrowAsync<RateLimitExceededException>(
            () => SendAsync(new MessageMyClientCommand(memberId, "and another")));
    }

    [Fact]
    public async Task A_busy_trainer_is_never_throttled_for_having_many_clients()
    {
        var ctx = await SeedAsync();
        var loud = await AddMemberAsync(ctx, "Loud");
        var quiet = await AddMemberAsync(ctx, "Quiet");
        await AssignAsync(ctx.TrainerId, loud);
        await AssignAsync(ctx.TrainerId, quiet);

        // A full hour's allowance spent on ONE client. The limit is per pairing, so the trainer's
        // other clients must be unaffected — otherwise coaching eleven people is itself the offence.
        for (var i = 0; i < CoachMessagePolicy.MaxMessagesPerHour; i++)
        {
            await MessageAsync(ctx, loud, CoachMessageAuthor.Trainer, $"msg {i}", readAt: null, sentAt: Now.AddMinutes(-i - 1));
        }

        AsTrainer(ctx);

        await SendAsync(new MessageMyClientCommand(quiet, "How did Thursday go?"));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        (await db.CoachMessages.IgnoreQueryFilters().CountAsync(c => c.MemberId == quiet)).ShouldBe(1);
    }

    [Fact]
    public async Task Being_replied_to_a_lot_does_not_use_up_your_own_allowance()
    {
        var ctx = await SeedAsync();
        var memberId = await AddMemberAsync(ctx, "Listener");
        await AssignAsync(ctx.TrainerId, memberId);

        // The MEMBER filled the hour. The trainer's own allowance is untouched by that.
        for (var i = 0; i < CoachMessagePolicy.MaxMessagesPerHour; i++)
        {
            await MessageAsync(ctx, memberId, CoachMessageAuthor.Member, $"msg {i}", readAt: null, sentAt: Now.AddMinutes(-i - 1));
        }

        AsTrainer(ctx);

        await SendAsync(new MessageMyClientCommand(memberId, "Let's slow down and take these one at a time."));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        (await db.CoachMessages.IgnoreQueryFilters()
            .CountAsync(c => c.MemberId == memberId && c.Author == CoachMessageAuthor.Trainer)).ShouldBe(1);
    }

    [Fact]
    public async Task Sending_nudges_the_member_and_not_the_sender()
    {
        var ctx = await SeedAsync();
        var memberId = await AddMemberWithLoginAsync(ctx, "Reachable");
        await AssignAsync(ctx.TrainerId, memberId);
        AsTrainer(ctx);

        await SendAsync(new MessageMyClientCommand(memberId, "Good session."));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var memberUserId = await db.Members.IgnoreQueryFilters()
            .Where(m => m.Id == memberId).Select(m => m.UserId).SingleAsync();

        // Exactly one nudge, to the member's own login. Pushing to the trainer would light up the
        // screen of the person who just typed it.
        CoachingNotifier.Notified.ShouldBe([memberUserId!.Value]);
    }

    [Fact]
    public async Task A_member_with_no_login_account_is_simply_not_pushed_to()
    {
        var ctx = await SeedAsync();
        // Plenty of gym members are records rather than users. That is not an error, and the message
        // must still send.
        var memberId = await AddMemberAsync(ctx, "NoLogin");
        await AssignAsync(ctx.TrainerId, memberId);
        AsTrainer(ctx);

        await SendAsync(new MessageMyClientCommand(memberId, "Good session."));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        CoachingNotifier.Notified.ShouldBeEmpty();
        (await db.CoachMessages.IgnoreQueryFilters().CountAsync(c => c.MemberId == memberId)).ShouldBe(1);
    }

    private async Task<Guid> AddMemberWithLoginAsync(SeedContext ctx, string firstName)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var user = new User
        {
            TenantId = ctx.TenantId, Email = $"{Guid.NewGuid():N}@example.com", PasswordHash = "unused-in-this-test",
            FirstName = firstName, LastName = "Client",
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var member = new Member
        {
            TenantId = ctx.TenantId, BranchId = ctx.BranchId, UserId = user.Id,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12], FirstName = firstName, LastName = "Client",
            Email = $"{Guid.NewGuid():N}@example.com", JoinDate = new DateOnly(2025, 1, 1),
        };
        db.Members.Add(member);
        await db.SaveChangesAsync();
        return member.Id;
    }

    private async Task AddCoachReplyTemplateAsync(SeedContext ctx)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        db.NotificationTemplates.Add(new NotificationTemplate
        {
            TenantId = ctx.TenantId, Code = "coach-reply", Category = NotificationCategory.CoachReply,
            Channel = NotificationChannel.InApp, Subject = "Your coach replied",
            BodyTemplate = "Open Coach to read it.",
        });
        await db.SaveChangesAsync();
    }

    private void AsTrainer(SeedContext ctx)
    {
        CurrentUser.TenantId = ctx.TenantId;
        CurrentUser.UserId = ctx.TrainerUserId;
        CurrentUser.IsAuthenticated = true;
    }

    private async Task MessageAsync(
        SeedContext ctx, Guid memberId, CoachMessageAuthor author, string body,
        DateTimeOffset? readAt, DateTimeOffset? sentAt = null)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        db.CoachMessages.Add(new CoachMessage
        {
            TenantId = ctx.TenantId, TrainerId = ctx.TrainerId, MemberId = memberId,
            Author = author, Body = body, SentAt = sentAt ?? Now.AddHours(-1), ReadAt = readAt,
        });
        await db.SaveChangesAsync();
    }

    private async Task AssignAsync(Guid trainerId, Guid memberId, bool isActive = true, DateOnly? startDate = null)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        // TrainerAssignment is ITenantScoped now and the tenant filter fails closed, so a fixture
        // that omits this seeds a row the query can never see. Derived from the trainer it belongs
        // to, so this helper keeps its two-argument shape.
        var tenantId = await db.Trainers.IgnoreQueryFilters()
            .Where(t => t.Id == trainerId).Select(t => t.TenantId).SingleAsync();

        db.TrainerAssignments.Add(new TrainerAssignment
        {
            TenantId = tenantId,
            TrainerId = trainerId, MemberId = memberId,
            StartDate = startDate ?? new DateOnly(2026, 1, 1), IsActive = isActive,
        });
        await db.SaveChangesAsync();
    }

    private async Task<Guid> AddMemberAsync(SeedContext ctx, string firstName)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var member = new Member
        {
            TenantId = ctx.TenantId, BranchId = ctx.BranchId,
            MemberCode = $"MBR-{Guid.NewGuid():N}"[..12], FirstName = firstName, LastName = "Client",
            Email = $"{Guid.NewGuid():N}@example.com", JoinDate = new DateOnly(2025, 1, 1),
        };
        db.Members.Add(member);
        await db.SaveChangesAsync();
        return member.Id;
    }

    private async Task<Guid> AddTrainerAsync(SeedContext ctx, string firstName)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();
        var user = new User
        {
            TenantId = ctx.TenantId, Email = $"{Guid.NewGuid():N}@example.com", PasswordHash = "unused-in-this-test",
            FirstName = firstName, LastName = "Coach",
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Trainer carries no name of its own — it is the User row that has one.
        var trainer = new Trainer { TenantId = ctx.TenantId, BranchId = ctx.BranchId, UserId = user.Id };
        db.Trainers.Add(trainer);
        await db.SaveChangesAsync();
        return trainer.Id;
    }

    private async Task<SeedContext> SeedAsync()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymOsDbContext>();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid():N}", Slug = Guid.NewGuid().ToString("N") };
        db.Tenants.Add(tenant);

        var branch = new Branch { TenantId = tenant.Id, Name = "Main", AddressLine = "1 Main St", City = "City", Country = "US" };
        db.Branches.Add(branch);

        var trainerUser = new User
        {
            TenantId = tenant.Id, Email = $"{Guid.NewGuid():N}@example.com", PasswordHash = "unused-in-this-test",
            FirstName = "Coach", LastName = "User",
        };
        var ownerUser = new User
        {
            TenantId = tenant.Id, Email = $"{Guid.NewGuid():N}@example.com", PasswordHash = "unused-in-this-test",
            FirstName = "Owner", LastName = "User",
        };
        db.Users.AddRange(trainerUser, ownerUser);
        await db.SaveChangesAsync();

        var trainer = new Trainer { TenantId = tenant.Id, BranchId = branch.Id, UserId = trainerUser.Id };
        db.Trainers.Add(trainer);
        await db.SaveChangesAsync();

        return new SeedContext(tenant.Id, branch.Id, trainer.Id, trainerUser.Id, ownerUser.Id);
    }

    private record SeedContext(Guid TenantId, Guid BranchId, Guid TrainerId, Guid TrainerUserId, Guid NonTrainerUserId);
}
