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

        return await db.ClassBookings.AsNoTracking()
            .Include(b => b.ClassSession!).ThenInclude(s => s.ClassType)
            .Include(b => b.ClassSession!).ThenInclude(s => s.Trainer!).ThenInclude(t => t.User)
            .Where(b => b.MemberId == memberId
                        && (b.Status == ClassBookingStatus.Booked || b.Status == ClassBookingStatus.Waitlisted)
                        // Belt to the write-side braces: session cancellation releases bookings, but
                        // rows stranded before that fix must still never render as classes to attend.
                        && b.ClassSession!.Status != ClassSessionStatus.Cancelled
                        && b.ClassSession!.StartsAt >= now)
            .OrderBy(b => b.ClassSession!.StartsAt)
            .Select(b => new MyClassBookingDto(
                b.Id,
                b.ClassSessionId,
                b.ClassSession!.ClassType!.Name,
                b.ClassSession.ClassType.ColorHex,
                b.ClassSession.Trainer == null ? null : b.ClassSession.Trainer.User!.FirstName + " " + b.ClassSession.Trainer.User.LastName,
                b.ClassSession.StartsAt,
                b.ClassSession.DurationMinutes,
                b.ClassSession.Location,
                b.Status))
            .ToListAsync(cancellationToken);
    }
}
