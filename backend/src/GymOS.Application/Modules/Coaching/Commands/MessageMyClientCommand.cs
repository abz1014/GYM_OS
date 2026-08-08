using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Coaching.Dtos;
using GymOS.Domain.Common;
using GymOS.Domain.Notifications;
using GymOS.Domain.Trainers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Coaching.Commands;

/// <summary>
/// The trainer's half of the conversation — without it a member sends into a void.
///
/// The acting trainer is resolved from the JWT, never taken from the caller, and the pairing between
/// that trainer and this member is checked before a word is written. Handing a memberId to a trainer
/// endpoint is safe only because of that check: it is what stops a trainer messaging a member who
/// is not their client.
/// </summary>
public record MessageMyClientCommand(Guid MemberId, string Body, Guid? WorkoutLogId = null) : ICommand<Guid>;

public class MessageMyClientCommandValidator : AbstractValidator<MessageMyClientCommand>
{
    public MessageMyClientCommandValidator()
    {
        RuleFor(x => x.MemberId).NotEmpty();
        RuleFor(x => x.Body)
            .Must(CoachMessagePolicy.IsSendable)
            .WithMessage($"Write something up to {CoachMessagePolicy.MaxBodyLength} characters.");
    }
}

public class MessageMyClientCommandHandler(
    IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<MessageMyClientCommand, Guid>
{
    public async Task<Guid> Handle(MessageMyClientCommand request, CancellationToken cancellationToken)
    {
        var trainerId = await MyTrainerResolver.ResolveTrainerIdAsync(db, currentUser, cancellationToken);
        var zone = await MyTrainerResolver.ResolveGymZoneAsync(db, trainerId, cancellationToken);
        var today = GymDay.Of(dateTimeProvider.UtcNow, zone);

        var pairing = await MyTrainerResolver.FindPairingAsync(db, trainerId, request.MemberId, cancellationToken);
        if (pairing is null || !CoachMessagePolicy.CanSend(pairing.Value.Start, pairing.Value.End, pairing.Value.IsActive, today))
        {
            throw new ForbiddenAccessException("This member is not one of your active clients.");
        }

        await GuardRateLimitAsync(trainerId, request.MemberId, CoachMessageAuthor.Trainer, cancellationToken);

        // The session must be the client's own. Without this a trainer could attach any workout in
        // the gym to a message and expose it to somebody it does not belong to.
        if (request.WorkoutLogId is Guid logId
            && !await db.WorkoutLogs.AnyAsync(w => w.Id == logId && w.MemberId == request.MemberId, cancellationToken))
        {
            throw new NotFoundException(nameof(GymOS.Domain.Workouts.WorkoutLog), logId);
        }

        var message = new CoachMessage
        {
            TrainerId = trainerId,
            MemberId = request.MemberId,
            Author = CoachMessageAuthor.Trainer,
            Body = CoachMessagePolicy.Normalise(request.Body),
            SentAt = dateTimeProvider.UtcNow,
            WorkoutLogId = request.WorkoutLogId
        };

        db.CoachMessages.Add(message);
        await ScheduleReplyNotificationAsync(request.MemberId, message.Id, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return message.Id;
    }

    /// <summary>
    /// Refuses a send once this side has filled its hourly allowance into this pairing.
    ///
    /// Scoped to the pairing and the author, so a trainer working through eleven clients is never
    /// throttled for being busy, and a member is never throttled because their coach replied a lot.
    /// The window is applied by CoachMessagePolicy; this only fetches what it needs to judge.
    /// </summary>
    private async Task GuardRateLimitAsync(
        Guid trainerId, Guid memberId, CoachMessageAuthor author, CancellationToken cancellationToken)
    {
        var since = dateTimeProvider.UtcNow - CoachMessagePolicy.RateLimitWindow;

        // Pulled to memory before the window compare: a DateTimeOffset range filter does not
        // translate on SQLite, the same limitation the rest of this codebase works around.
        var recent = (await db.CoachMessages.AsNoTracking()
                .Where(c => c.TrainerId == trainerId && c.MemberId == memberId && c.Author == author)
                .Select(c => c.SentAt)
                .ToListAsync(cancellationToken))
            .Where(sentAt => sentAt > since)
            .ToList();

        if (!CoachMessagePolicy.IsWithinRateLimit(recent, dateTimeProvider.UtcNow))
        {
            throw new RateLimitExceededException(
                $"That's {CoachMessagePolicy.MaxMessagesPerHour} messages in an hour. Give it a little while before sending more.");
        }
    }

    /// <summary>
    /// Tells the member their coach has written, through the same ScheduledNotification pipeline
    /// every other alert in this system uses rather than a side channel of its own.
    ///
    /// **What this does and does not do.** The template is InApp, so dispatch records it and the
    /// member sees it inside the product; it is not a push notification and this system has no way
    /// to send one — there is no native app, and email/SMS remain no-op stubs until a provider is
    /// configured. The member still finds out by opening GymOS. What changed is that they no longer
    /// have to go looking: the unread count rides on their home screen (GetMyTodayQuery).
    ///
    /// Failure to schedule must never lose the message. The notification is a courtesy; the message
    /// is the thing the trainer wrote, and a missing template is not a reason to reject it. So a
    /// missing template is skipped silently rather than thrown — the conversation is already
    /// readable without it.
    /// </summary>
    private async Task ScheduleReplyNotificationAsync(Guid memberId, Guid messageId, CancellationToken cancellationToken)
    {
        var template = await db.NotificationTemplates
            .FirstOrDefaultAsync(t => t.Category == NotificationCategory.CoachReply && t.IsActive, cancellationToken);

        if (template is null)
        {
            return;
        }

        var member = await db.Members.AsNoTracking()
            .Where(m => m.Id == memberId)
            .Select(m => new { m.TenantId, m.BranchId })
            .FirstOrDefaultAsync(cancellationToken);

        if (member is null)
        {
            return;
        }

        db.ScheduledNotifications.Add(new ScheduledNotification
        {
            TenantId = member.TenantId,
            BranchId = member.BranchId,
            NotificationTemplateId = template.Id,
            RecipientMemberId = memberId,
            ScheduledFor = dateTimeProvider.UtcNow,
            Status = ScheduledNotificationStatus.Pending,
            RelatedEntityType = nameof(CoachMessage),
            RelatedEntityId = messageId
        });
    }
}

/// <summary>Marks what this client has written as read. Idempotent.</summary>
public record ReadMyClientMessagesCommand(Guid MemberId) : ICommand<Unit>;

public class ReadMyClientMessagesCommandHandler(
    IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<ReadMyClientMessagesCommand, Unit>
{
    public async Task<Unit> Handle(ReadMyClientMessagesCommand request, CancellationToken cancellationToken)
    {
        var trainerId = await MyTrainerResolver.ResolveTrainerIdAsync(db, currentUser, cancellationToken);

        // Reading is allowed for an ended pairing too — a trainer wrapping up should still be able to
        // clear what a former client sent them.
        if (await MyTrainerResolver.FindPairingAsync(db, trainerId, request.MemberId, cancellationToken) is null)
        {
            throw new ForbiddenAccessException("This member is not one of your clients.");
        }

        var unread = await db.CoachMessages
            .Where(m => m.TrainerId == trainerId && m.MemberId == request.MemberId
                        && m.Author == CoachMessageAuthor.Member && m.ReadAt == null)
            .ToListAsync(cancellationToken);

        foreach (var message in unread)
        {
            message.ReadAt = dateTimeProvider.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

/// <summary>
/// One client's conversation, as the trainer sees it. Windowed the same way the member's is: a long
/// coaching relationship is thousands of messages and no screen opens on the first one.
/// </summary>
public record GetMyClientConversationQuery(Guid MemberId, int Take = 50) : IQuery<CoachConversationDto>;

public class GetMyClientConversationQueryHandler(
    IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetMyClientConversationQuery, CoachConversationDto>
{
    public async Task<CoachConversationDto> Handle(GetMyClientConversationQuery request, CancellationToken cancellationToken)
    {
        var trainerId = await MyTrainerResolver.ResolveTrainerIdAsync(db, currentUser, cancellationToken);
        var zone = await MyTrainerResolver.ResolveGymZoneAsync(db, trainerId, cancellationToken);
        var today = GymDay.Of(dateTimeProvider.UtcNow, zone);

        var pairing = await MyTrainerResolver.FindPairingAsync(db, trainerId, request.MemberId, cancellationToken)
            ?? throw new ForbiddenAccessException("This member is not one of your clients.");

        var memberName = await db.Members.AsNoTracking()
            .Where(m => m.Id == request.MemberId)
            .Select(m => m.FirstName + " " + m.LastName)
            .FirstOrDefaultAsync(cancellationToken) ?? "Member";

        var all = (await db.CoachMessages.AsNoTracking()
                .Where(m => m.TrainerId == trainerId && m.MemberId == request.MemberId)
                .Select(m => new { m.Id, m.Author, m.Body, m.SentAt, m.ReadAt, m.WorkoutLogId })
                .ToListAsync(cancellationToken))
            .OrderBy(m => m.SentAt)
            .ToList();

        var take = Math.Clamp(request.Take, 1, 200);
        var window = all.Skip(Math.Max(0, all.Count - take)).ToList();

        return new CoachConversationDto(
            request.MemberId,
            memberName,
            CoachMessagePolicy.CanSend(pairing.Start, pairing.End, pairing.IsActive, today),
            CoachMessagePolicy.UnreadFor(CoachMessageAuthor.Trainer, all.Select(m => (m.Author, m.ReadAt))),
            all.Count > window.Count,
            window
                .Select(m => new CoachMessageDto(
                    m.Id, m.Author.ToString(), m.Body, m.SentAt, m.ReadAt is not null, m.WorkoutLogId))
                .ToList());
    }
}
