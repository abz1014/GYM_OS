using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Coaching.Dtos;
using GymOS.Domain.Trainers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Coaching.Queries;

/// <summary>
/// The acting trainer's own clients, ordered by who is waiting on a reply.
///
/// This is the query the messaging work has been missing. Both halves of the conversation have
/// existed for a while — a member could write to their coach and a coach could write back — but the
/// trainer's side was reachable only by knowing a member id and calling the API by hand, because
/// nothing would tell a trainer WHICH of their clients had written. A thread you cannot find is not
/// a feature, and that is why this had to come before any screen.
///
/// The trainer is resolved from the JWT via MyTrainerResolver and never accepted from the caller, so
/// this cannot become "show me anybody's roster". Ended pairings are included: a coach should be
/// able to look back at what they told someone months ago, and CoachMessagePolicy — not this query —
/// is what stops them writing anything new into a finished pairing.
///
/// Ordering is deliberate. Anyone with unread messages comes first, because the whole point of the
/// list is to answer "who needs me"; within that, most recent first. Clients who have never
/// exchanged a word sort last by name rather than being hidden, since starting the conversation is
/// the other thing a trainer opens this screen to do.
/// </summary>
public record GetMyClientsQuery : IQuery<IReadOnlyList<MyClientRowDto>>;

public class GetMyClientsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetMyClientsQuery, IReadOnlyList<MyClientRowDto>>
{
    /// <summary>Enough of the last message to recognise the conversation, not enough to read it here.</summary>
    private const int PreviewLength = 80;

    public async Task<IReadOnlyList<MyClientRowDto>> Handle(GetMyClientsQuery request, CancellationToken cancellationToken)
    {
        var trainerId = await MyTrainerResolver.ResolveTrainerIdAsync(db, currentUser, cancellationToken);

        // One row per member, not per assignment: a client who was paired, released and re-signed has
        // several TrainerAssignment rows and is still one person with one conversation. The latest
        // assignment decides whether the pairing is live.
        var assignments = await db.TrainerAssignments.AsNoTracking()
            .Where(a => a.TrainerId == trainerId)
            .Select(a => new { a.MemberId, a.StartDate, a.IsActive })
            .ToListAsync(cancellationToken);

        if (assignments.Count == 0)
        {
            return [];
        }

        var latestPerMember = assignments
            .GroupBy(a => a.MemberId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.StartDate).First());

        var memberIds = latestPerMember.Keys.ToList();

        var members = await db.Members.AsNoTracking()
            .Where(m => memberIds.Contains(m.Id))
            .Select(m => new { m.Id, m.FirstName, m.LastName, m.MemberCode })
            .ToListAsync(cancellationToken);

        var messages = await db.CoachMessages.AsNoTracking()
            .Where(c => c.TrainerId == trainerId && memberIds.Contains(c.MemberId))
            .Select(c => new { c.MemberId, c.Author, c.Body, c.SentAt, c.ReadAt })
            .ToListAsync(cancellationToken);

        var byMember = messages.GroupBy(c => c.MemberId).ToDictionary(g => g.Key, g => g.ToList());

        var rows = members.Select(m =>
        {
            var thread = byMember.GetValueOrDefault(m.Id) ?? [];
            var last = thread.OrderByDescending(c => c.SentAt).FirstOrDefault();

            // Only the member's own unread messages count. A trainer's unread outbound message means
            // the member has not opened it yet, which is the member's business and would otherwise
            // light up the trainer's own inbox for something they wrote themselves.
            var unread = thread.Count(c => c.Author == CoachMessageAuthor.Member && c.ReadAt is null);

            return new MyClientRowDto(
                m.Id,
                $"{m.FirstName} {m.LastName}",
                m.MemberCode,
                latestPerMember[m.Id].IsActive,
                unread,
                last?.SentAt,
                last is null ? null : Preview(last.Body));
        });

        return rows
            .OrderByDescending(r => r.UnreadFromMember > 0)
            .ThenByDescending(r => r.LastMessageAt ?? DateTimeOffset.MinValue)
            .ThenBy(r => r.MemberName)
            .ToList();
    }

    private static string Preview(string body)
        => body.Length <= PreviewLength ? body : string.Concat(body.AsSpan(0, PreviewLength).TrimEnd(), "…");
}
