using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Common;
using GymOS.Domain.Trainers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Portal.Commands;

/// <summary>
/// A member writing to their own trainer, optionally about a specific session.
///
/// The trainer is resolved from the member's own active assignment and never accepted from the
/// caller — the same rule every other /api/me command follows, and the one that keeps this from
/// becoming a way to send free text to any trainer in the gym. A member with no active pairing is
/// refused rather than silently writing a message nobody will read.
/// </summary>
public record MessageMyCoachCommand(string Body, Guid? WorkoutLogId = null) : ICommand<Guid>;

public class MessageMyCoachCommandValidator : AbstractValidator<MessageMyCoachCommand>
{
    public MessageMyCoachCommandValidator()
    {
        RuleFor(x => x.Body)
            .Must(CoachMessagePolicy.IsSendable)
            .WithMessage($"Write something up to {CoachMessagePolicy.MaxBodyLength} characters.");
    }
}

public class MessageMyCoachCommandHandler(
    IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<MessageMyCoachCommand, Guid>
{
    public async Task<Guid> Handle(MessageMyCoachCommand request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);
        var zone = await MyMemberResolver.ResolveGymZoneAsync(db, memberId, cancellationToken);
        var today = GymDay.Of(dateTimeProvider.UtcNow, zone);

        var assignment = (await db.TrainerAssignments.AsNoTracking()
                .Where(a => a.MemberId == memberId)
                .Select(a => new { a.TrainerId, a.StartDate, a.EndDate, a.IsActive })
                .ToListAsync(cancellationToken))
            .OrderByDescending(a => a.StartDate)
            .FirstOrDefault();

        if (assignment is null
            || !CoachMessagePolicy.CanSend(assignment.StartDate, assignment.EndDate, assignment.IsActive, today))
        {
            throw new ForbiddenAccessException("You don't have a trainer to message right now.");
        }

        // A session reference is only honoured when it is the member's own. Trusting the id would let
        // a member point their coach at somebody else's workout.
        if (request.WorkoutLogId is Guid logId
            && !await db.WorkoutLogs.AnyAsync(w => w.Id == logId && w.MemberId == memberId, cancellationToken))
        {
            throw new NotFoundException(nameof(GymOS.Domain.Workouts.WorkoutLog), logId);
        }

        var message = new CoachMessage
        {
            TrainerId = assignment.TrainerId,
            MemberId = memberId,
            Author = CoachMessageAuthor.Member,
            Body = CoachMessagePolicy.Normalise(request.Body),
            SentAt = dateTimeProvider.UtcNow,
            WorkoutLogId = request.WorkoutLogId
        };

        db.CoachMessages.Add(message);
        await db.SaveChangesAsync(cancellationToken);
        return message.Id;
    }
}

/// <summary>Marks everything the trainer has written as read. Idempotent.</summary>
public record ReadMyCoachMessagesCommand : ICommand<Unit>;

public class ReadMyCoachMessagesCommandHandler(
    IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<ReadMyCoachMessagesCommand, Unit>
{
    public async Task<Unit> Handle(ReadMyCoachMessagesCommand request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);

        // Only the trainer's side is marked: a member cannot mark their own messages as read on the
        // trainer's behalf, which would quietly clear the badge on the other side of the conversation.
        var unread = await db.CoachMessages
            .Where(m => m.MemberId == memberId && m.Author == CoachMessageAuthor.Trainer && m.ReadAt == null)
            .ToListAsync(cancellationToken);

        foreach (var message in unread)
        {
            message.ReadAt = dateTimeProvider.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
