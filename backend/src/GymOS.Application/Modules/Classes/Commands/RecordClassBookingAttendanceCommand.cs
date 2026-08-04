using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Classes;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Classes.Commands;

/// <summary>
/// Resolves a confirmed booking's attendance at the door: checked in (attended) or no-show.
/// Deliberately does NOT promote from the waitlist on a no-show — a no-show is recorded once the
/// session is under way, when backfilling from the waitlist no longer makes sense (that only
/// happens on a pre-session cancellation).
/// </summary>
public record RecordClassBookingAttendanceCommand(Guid ClassBookingId, bool Attended) : ICommand<Unit>;

public class RecordClassBookingAttendanceCommandHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<RecordClassBookingAttendanceCommand, Unit>
{
    public async Task<Unit> Handle(RecordClassBookingAttendanceCommand request, CancellationToken cancellationToken)
    {
        var booking = await db.ClassBookings.FirstOrDefaultAsync(b => b.Id == request.ClassBookingId, cancellationToken)
            ?? throw new NotFoundException(nameof(ClassBooking), request.ClassBookingId);

        if (booking.Status is not (ClassBookingStatus.Booked or ClassBookingStatus.CheckedIn))
        {
            throw new ValidationException("Only a confirmed booking can be checked in or marked a no-show.");
        }

        if (request.Attended)
        {
            booking.Status = ClassBookingStatus.CheckedIn;
            booking.CheckedInAt = dateTimeProvider.UtcNow;
        }
        else
        {
            booking.Status = ClassBookingStatus.NoShow;
            booking.CheckedInAt = null;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
