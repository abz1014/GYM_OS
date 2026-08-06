using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Coaching.Dtos;
using GymOS.Domain.Common;
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
        await db.SaveChangesAsync(cancellationToken);
        return message.Id;
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
