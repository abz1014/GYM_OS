using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Portal.Dtos;
using GymOS.Domain.Classes;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Portal.Queries;

/// <summary>
/// The member's own upcoming class bookings (booked or waitlisted), for a "your classes" view.
/// Resolves the member server-side; never trusts a caller-supplied id.
/// </summary>
public record GetMyClassBookingsQuery : IQuery<List<MyClassBookingDto>>;

public class GetMyClassBookingsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetMyClassBookingsQuery, List<MyClassBookingDto>>
{
    public async Task<List<MyClassBookingDto>> Handle(GetMyClassBookingsQuery request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);
        var now = dateTimeProvider.UtcNow;

        var bookings = await db.ClassBookings.AsNoTracking()
            .Include(b => b.ClassSession!).ThenInclude(s => s.ClassType)
            .Include(b => b.ClassSession!).ThenInclude(s => s.Trainer!).ThenInclude(t => t.User)
            .Where(b => b.MemberId == memberId
                        && (b.Status == ClassBookingStatus.Booked || b.Status == ClassBookingStatus.Waitlisted)
                        // Belt to the write-side braces: session cancellation releases bookings, but
                        // rows stranded before that fix must still never render as classes to attend.
                        && b.ClassSession!.Status != ClassSessionStatus.Cancelled)
            .Select(b => new MyClassBookingDto(
                b.Id,
                b.ClassSessionId,
                b.ClassSession!.ClassType!.Name,
                b.ClassSession.ClassType.ColorHex,
                b.ClassSession.Trainer == null ? null : b.ClassSession.Trainer.User!.FirstName + " " + b.ClassSession.Trainer.User.LastName,
                b.ClassSession.StartsAt,
                b.ClassSession.DurationMinutes,
                b.ClassSession.Location,
                b.Status,
                // Filled in below — a queue position cannot be projected in SQL here, because it is
                // an ordering over BookedAt (a DateTimeOffset) across OTHER members' bookings.
                (int?)null))
            .ToListAsync(cancellationToken);

        /*
         * "Still ahead" and "soonest first" are decided here rather than in SQL, and that move is a
         * fix, not a preference. Both touch StartsAt across the booking->session join, and the SQLite
         * provider the whole test suite runs on cannot translate a DateTimeOffset comparison — the
         * query threw InvalidOperationException the moment anything tried to exercise it, which is
         * precisely why this endpoint had no test while GetMyTodayQuery (which already reduced in
         * memory, and says so) had several. Same rows, same order, now pinnable.
         */
        var upcoming = bookings
            .Where(b => b.StartsAt >= now)
            .OrderBy(b => b.StartsAt)
            .ToList();

        return await WaitlistPositionResolver.FillAsync(db, upcoming, cancellationToken);
    }
}
