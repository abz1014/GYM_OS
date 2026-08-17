using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Portal.Dtos;
using GymOS.Domain.Classes;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Portal.Queries;

/// <summary>
/// The classes the member has already been to, most recent first.
///
/// The counterpart to GetMyClassBookingsQuery, which deliberately shows only what is still ahead —
/// so the moment a session started, it fell off every screen the member had and the portal could no
/// longer answer "did I actually go on Tuesday". This is that answer, and it is the honest one:
/// CheckedIn and NoShow are both shown, because a member's own attendance record is not something
/// to flatter them with.
///
/// Cancelled bookings are excluded. A booking the member released is not a class they attended, and
/// listing it as history would make a tidy calendar look like a training record.
/// </summary>
public record GetMyClassHistoryQuery : IQuery<List<MyClassHistoryDto>>;

public class GetMyClassHistoryQueryHandler(
    IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetMyClassHistoryQuery, List<MyClassHistoryDto>>
{
    private const int Take = 20;

    public async Task<List<MyClassHistoryDto>> Handle(GetMyClassHistoryQuery request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);
        var now = dateTimeProvider.UtcNow;

        var bookings = await db.ClassBookings.AsNoTracking()
            .Where(b => b.MemberId == memberId
                        && (b.Status == ClassBookingStatus.Booked
                            || b.Status == ClassBookingStatus.CheckedIn
                            || b.Status == ClassBookingStatus.NoShow))
            .Select(b => new MyClassHistoryDto(
                b.ClassSession!.ClassType!.Name,
                b.ClassSession.StartsAt,
                b.ClassSession.DurationMinutes,
                b.Status))
            .ToListAsync(cancellationToken);

        // "Past" compares StartsAt, a DateTimeOffset, which SQLite can neither compare nor order —
        // so both the cut and the sort happen in memory, the same way GetMyTodayQuery handles the
        // mirror-image "still upcoming" test. A member's own bookings are a small set.
        return bookings
            .Where(b => b.StartsAt < now)
            .OrderByDescending(b => b.StartsAt)
            .Take(Take)
            .ToList();
    }
}
