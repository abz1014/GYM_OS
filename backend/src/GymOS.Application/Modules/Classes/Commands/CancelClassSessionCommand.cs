using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Classes;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Classes.Commands;

/// <summary>
/// Cancel a single dated session (public holiday, instructor sick) without touching its recurring
/// schedule — the rest of the weekly slot keeps running. Every still-active booking (confirmed or
/// waitlisted) is released too, since the class isn't happening; there is no waitlist promotion
/// here, because the session itself is gone.
/// </summary>
public record CancelClassSessionCommand(Guid ClassSessionId) : ICommand<Unit>;

public class CancelClassSessionCommandHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<CancelClassSessionCommand, Unit>
{
    public async Task<Unit> Handle(CancelClassSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await db.ClassSessions.FirstOrDefaultAsync(s => s.Id == request.ClassSessionId, cancellationToken)
            ?? throw new NotFoundException(nameof(ClassSession), request.ClassSessionId);

        if (session.Status == ClassSessionStatus.Completed)
        {
            throw new ValidationException("A session that has already run cannot be cancelled.");
        }

        session.Status = ClassSessionStatus.Cancelled;

        var activeBookings = await db.ClassBookings
            .Where(b => b.ClassSessionId == session.Id
                        && (b.Status == ClassBookingStatus.Booked || b.Status == ClassBookingStatus.Waitlisted))
            .ToListAsync(cancellationToken);

        foreach (var booking in activeBookings)
        {
            booking.Status = ClassBookingStatus.Cancelled;
            booking.CancelledAt = dateTimeProvider.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
