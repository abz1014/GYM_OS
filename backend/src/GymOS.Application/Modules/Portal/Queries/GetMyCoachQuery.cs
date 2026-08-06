using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Portal.Dtos;
using GymOS.Domain.Common;
using GymOS.Domain.Trainers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Portal.Queries;

/// <summary>
/// The member's coach and everything said between them, oldest first.
///
/// Returns a conversation even when the pairing has ended: what a coach told a member is the
/// member's record of their own training, and losing it when an assignment lapses would be taking
/// something away. <see cref="MyCoachDto.CanSend"/> is what closes — you can read it, you cannot add
/// to it. Self-scoped via MyMemberResolver; no member id is accepted from the caller.
/// </summary>
public record GetMyCoachQuery : IQuery<MyCoachDto>;

public class GetMyCoachQueryHandler(
    IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetMyCoachQuery, MyCoachDto>
{
    public async Task<MyCoachDto> Handle(GetMyCoachQuery request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);
        var zone = await MyMemberResolver.ResolveGymZoneAsync(db, memberId, cancellationToken);
        var today = GymDay.Of(dateTimeProvider.UtcNow, zone);

        // The most recent pairing, whether or not it still runs — an ended one still owns its history.
        var assignment = (await db.TrainerAssignments.AsNoTracking()
                .Where(a => a.MemberId == memberId)
                .Select(a => new
                {
                    a.TrainerId,
                    a.StartDate,
                    a.EndDate,
                    a.IsActive,
                    TrainerName = a.Trainer!.User!.FirstName + " " + a.Trainer.User.LastName
                })
                .ToListAsync(cancellationToken))
            .OrderByDescending(a => a.StartDate)
            .FirstOrDefault();

        if (assignment is null)
        {
            return new MyCoachDto(null, null, false, 0, []);
        }

        var messages = (await db.CoachMessages.AsNoTracking()
                .Where(m => m.MemberId == memberId && m.TrainerId == assignment.TrainerId)
                .Select(m => new { m.Id, m.Author, m.Body, m.SentAt, m.ReadAt, m.WorkoutLogId })
                .ToListAsync(cancellationToken))
            .OrderBy(m => m.SentAt)
            .ToList();

        return new MyCoachDto(
            assignment.TrainerId,
            assignment.TrainerName,
            CoachMessagePolicy.CanSend(assignment.StartDate, assignment.EndDate, assignment.IsActive, today),
            CoachMessagePolicy.UnreadFor(CoachMessageAuthor.Member, messages.Select(m => (m.Author, m.ReadAt))),
            messages
                .Select(m => new MyCoachMessageDto(
                    m.Id, m.Author.ToString(), m.Body, m.SentAt, m.ReadAt is not null, m.WorkoutLogId))
                .ToList());
    }
}
