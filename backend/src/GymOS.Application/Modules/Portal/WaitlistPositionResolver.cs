using GymOS.Application.Common.Interfaces;
using GymOS.Application.Modules.Portal.Dtos;
using GymOS.Domain.Classes;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Portal;

/// <summary>
/// Fills in "you are Nth in line" on a member's waitlisted bookings.
///
/// Without it the portal could only say "Waitlisted", which answers the wrong question: a member
/// deciding whether to keep the evening free needs to know if they are next up or eleventh, and the
/// difference between those two is the difference between waiting and making other plans. The
/// promotion rule this reports on is real — releasing a confirmed spot promotes the earliest
/// waitlisted booking — so the position is simply that queue's own order made visible.
///
/// Ordered by BookedAt, which is a DateTimeOffset, so the ranking is done in memory: SQLite (the
/// test harness's provider) cannot order or compare that type, and a queue that is only correct on
/// Postgres is a queue nothing pins. The set is one session's waitlist, not a table scan.
/// </summary>
internal static class WaitlistPositionResolver
{
    public static async Task<List<MyClassBookingDto>> FillAsync(
        IApplicationDbContext db, List<MyClassBookingDto> bookings, CancellationToken cancellationToken)
    {
        var waitlistedSessionIds = bookings
            .Where(b => b.Status == ClassBookingStatus.Waitlisted)
            .Select(b => b.SessionId)
            .Distinct()
            .ToList();

        // The overwhelmingly common case: nothing is waitlisted, so nothing is queried.
        if (waitlistedSessionIds.Count == 0)
        {
            return bookings;
        }

        var queue = await db.ClassBookings.AsNoTracking()
            .Where(b => waitlistedSessionIds.Contains(b.ClassSessionId) && b.Status == ClassBookingStatus.Waitlisted)
            .Select(b => new { b.Id, b.ClassSessionId, b.BookedAt })
            .ToListAsync(cancellationToken);

        var positions = queue
            .GroupBy(b => b.ClassSessionId)
            .SelectMany(g => g.OrderBy(b => b.BookedAt).Select((b, index) => (b.Id, Position: index + 1)))
            .ToDictionary(x => x.Id, x => x.Position);

        // 1-based on purpose. "You are 0th on the waitlist" is not a sentence anyone says, and a
        // zero-based position read as a count would tell the person at the front they are behind
        // nobody AND behind someone, depending on which screen rendered it.
        return bookings
            .Select(b => b.Status == ClassBookingStatus.Waitlisted && positions.TryGetValue(b.BookingId, out var position)
                ? b with { WaitlistPosition = position }
                : b)
            .ToList();
    }
}
