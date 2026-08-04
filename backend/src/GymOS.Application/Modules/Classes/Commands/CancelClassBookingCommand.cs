using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Classes;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ValidationException = FluentValidation.ValidationException;

namespace GymOS.Application.Modules.Classes.Commands;

/// <summary>
/// Releases a booking. If it was a confirmed spot (not a waitlist place), freeing it promotes the
/// longest-waiting member off the waitlist into the open spot — the single most important behaviour
/// of a real booking system, since a full-then-cancelled class should backfill itself automatically.
/// </summary>
public record CancelClassBookingCommand(Guid ClassBookingId) : ICommand<Unit>;

public class CancelClassBookingCommandHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<CancelClassBookingCommand, Unit>
{
    public async Task<Unit> Handle(CancelClassBookingCommand request, CancellationToken cancellationToken)
    {
        var booking = await db.ClassBookings.FirstOrDefaultAsync(b => b.Id == request.ClassBookingId, cancellationToken)
            ?? throw new NotFoundException(nameof(ClassBooking), request.ClassBookingId);

        if (booking.Status is not (ClassBookingStatus.Booked or ClassBookingStatus.Waitlisted))
        {
            throw new ValidationException("Only an active booking or waitlist place can be cancelled.");
        }

        var freedAConfirmedSpot = ClassBookingPolicy.Occupies(booking.Status);

        booking.Status = ClassBookingStatus.Cancelled;
        booking.CancelledAt = dateTimeProvider.UtcNow;

        if (freedAConfirmedSpot)
        {
            var session = await db.ClassSessions.FirstAsync(s => s.Id == booking.ClassSessionId, cancellationToken);

            var occupiedAfter = await db.ClassBookings.CountAsync(
                b => b.ClassSessionId == session.Id && b.Id != booking.Id
                     && (b.Status == ClassBookingStatus.Booked || b.Status == ClassBookingStatus.CheckedIn),
                cancellationToken);

            if (ClassBookingPolicy.CanPromoteFromWaitlist(occupiedAfter, session.Capacity))
            {
                // Order the FIFO waitlist in memory: the set is bounded by capacity+overflow, and it
                // keeps the BookedAt (DateTimeOffset) ordering off the DB (SQLite can't ORDER BY it).
                var waitlist = await db.ClassBookings
                    .Where(b => b.ClassSessionId == session.Id && b.Status == ClassBookingStatus.Waitlisted)
                    .ToListAsync(cancellationToken);

                var nextUp = waitlist.OrderBy(b => b.BookedAt).FirstOrDefault();
                if (nextUp is not null)
                {
                    nextUp.Status = ClassBookingStatus.Booked;
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
