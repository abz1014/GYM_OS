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
/// <param name="Take">How many of the most recent messages to return. Bounded because a
/// conversation grows without limit and a phone should not download a year of it to show the last
/// exchange — 5,000 messages was a 1.1 MB response.</param>
public record GetMyCoachQuery(int Take = GetMyCoachQueryHandler.DefaultWindow) : IQuery<MyCoachDto>;

public class GetMyCoachQueryHandler(
    IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetMyCoachQuery, MyCoachDto>
{
    /// <summary>Enough to cover any recent exchange without shipping the archive.</summary>
    public const int DefaultWindow = 50;

    public const int MaxWindow = 200;


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
            return new MyCoachDto(null, null, false, 0, false, []);
        }

        // Reduced in memory for the reason every DateTimeOffset ordering in this codebase is: SQLite,
        // the test provider, cannot order one in SQL. The window is applied after sorting, so what
        // comes back is genuinely the most recent exchange rather than an arbitrary slice.
        var all = (await db.CoachMessages.AsNoTracking()
                .Where(m => m.MemberId == memberId && m.TrainerId == assignment.TrainerId)
                .Select(m => new { m.Id, m.Author, m.Body, m.SentAt, m.ReadAt, m.WorkoutLogId })
                .ToListAsync(cancellationToken))
            .OrderBy(m => m.SentAt)
            .ToList();

        var take = Math.Clamp(request.Take, 1, MaxWindow);
        var messages = all.Skip(Math.Max(0, all.Count - take)).ToList();

        return new MyCoachDto(
            assignment.TrainerId,
            assignment.TrainerName,
            CoachMessagePolicy.CanSend(assignment.StartDate, assignment.EndDate, assignment.IsActive, today),
            // Counted across everything, not just the window — an unread message that scrolled out of
            // sight is still unread, and a badge that quietly drops it is worse than no badge.
            CoachMessagePolicy.UnreadFor(CoachMessageAuthor.Member, all.Select(m => (m.Author, m.ReadAt))),
            all.Count > messages.Count,
            messages
                .Select(m => new MyCoachMessageDto(
                    m.Id, m.Author.ToString(), m.Body, m.SentAt, m.ReadAt is not null, m.WorkoutLogId))
                .ToList());
    }
}
