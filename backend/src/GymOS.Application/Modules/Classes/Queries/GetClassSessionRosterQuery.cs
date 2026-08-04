using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Common.Security;
using GymOS.Application.Modules.Classes.Dtos;
using GymOS.Domain.Classes;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Classes.Queries;

/// <summary>
/// The full booking list for one session — the staff roster. Ordered confirmed-first, then the
/// waitlist in FIFO order (so the top waitlisted member is visibly next in line), then the
/// no-shows/cancellations. Branch-access scoped like every other operational read.
/// </summary>
public record GetClassSessionRosterQuery(Guid ClassSessionId) : IQuery<ClassSessionRosterDto>;

public class GetClassSessionRosterQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetClassSessionRosterQuery, ClassSessionRosterDto>
{
    public async Task<ClassSessionRosterDto> Handle(GetClassSessionRosterQuery request, CancellationToken cancellationToken)
    {
        var accessibleBranchIds = await BranchAccessResolver.GetAccessibleBranchIdsAsync(db, currentUser, cancellationToken);

        var session = await db.ClassSessions.AsNoTracking()
            .Include(s => s.ClassType)
            .Where(s => accessibleBranchIds.Contains(s.BranchId))
            .FirstOrDefaultAsync(s => s.Id == request.ClassSessionId, cancellationToken)
            ?? throw new NotFoundException(nameof(ClassSession), request.ClassSessionId);

        // Fetch then order/project in memory: one session's roster is a small set, and it keeps the
        // BookedAt (DateTimeOffset) tiebreak off the DB, which SQLite can't ORDER BY.
        var raw = await db.ClassBookings.AsNoTracking()
            .Include(b => b.Member)
            .Where(b => b.ClassSessionId == request.ClassSessionId)
            .ToListAsync(cancellationToken);

        var bookings = raw
            // Confirmed (0) → waitlisted (1) → resolved (2), each then oldest-first so the waitlist
            // reads as a queue.
            .OrderBy(b => b.Status is ClassBookingStatus.Booked or ClassBookingStatus.CheckedIn ? 0
                : b.Status == ClassBookingStatus.Waitlisted ? 1 : 2)
            .ThenBy(b => b.BookedAt)
            .Select(b => new ClassBookingDto(
                b.Id, b.MemberId, b.Member!.FirstName + " " + b.Member.LastName, b.Member.MemberCode,
                b.Status, b.BookedAt, b.CheckedInAt))
            .ToList();

        var bookedCount = bookings.Count(b => b.Status is ClassBookingStatus.Booked or ClassBookingStatus.CheckedIn);
        var waitlistCount = bookings.Count(b => b.Status == ClassBookingStatus.Waitlisted);

        return new ClassSessionRosterDto(
            session.Id, session.ClassTypeId, session.ClassType!.Name, session.StartsAt, session.Capacity,
            bookedCount, waitlistCount, session.Status, bookings);
    }
}
