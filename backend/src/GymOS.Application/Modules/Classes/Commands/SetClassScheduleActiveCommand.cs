using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Classes;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Classes.Commands;

/// <summary>
/// Discontinue or reinstate a recurring slot. Deactivating isn't just a flag flip — it pulls the
/// slot's still-upcoming sessions off the calendar (cancelled), because "we stopped running Monday
/// Spin" should mean members can no longer see or book its future instances. Reactivating
/// regenerates the booking window so the slot is immediately useful again.
/// </summary>
public record SetClassScheduleActiveCommand(Guid ClassScheduleId, bool IsActive) : ICommand<Unit>;

public class SetClassScheduleActiveCommandHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<SetClassScheduleActiveCommand, Unit>
{
    public async Task<Unit> Handle(SetClassScheduleActiveCommand request, CancellationToken cancellationToken)
    {
        var schedule = await db.ClassSchedules.FirstOrDefaultAsync(s => s.Id == request.ClassScheduleId, cancellationToken)
            ?? throw new NotFoundException(nameof(ClassSchedule), request.ClassScheduleId);

        var now = dateTimeProvider.UtcNow;

        if (!request.IsActive)
        {
            schedule.IsActive = false;

            // Status filters in SQL; the StartsAt cut reduces in memory — the DateTimeOffset
            // comparison SQLite cannot translate, and a slot's Scheduled set is bounded by the
            // generation window anyway. The same trade every query over StartsAt here makes.
            var scheduled = await db.ClassSessions
                .Where(s => s.ClassScheduleId == schedule.Id && s.Status == ClassSessionStatus.Scheduled)
                .ToListAsync(cancellationToken);
            var upcoming = scheduled.Where(s => s.StartsAt > now).ToList();

            foreach (var session in upcoming)
            {
                session.Status = ClassSessionStatus.Cancelled;
            }

            /*
             * Release the people, not just the slots. Cancelling the sessions without their bookings
             * left members holding live Booked rows on classes that no longer ran — the Today page
             * (which reads booking status, correctly) kept telling them to turn up. Same release
             * CancelClassSessionCommand performs for a single session, applied to the whole slot.
             */
            var sessionIds = upcoming.Select(s => s.Id).ToList();
            var strandedBookings = await db.ClassBookings
                .Where(b => sessionIds.Contains(b.ClassSessionId)
                            && (b.Status == ClassBookingStatus.Booked || b.Status == ClassBookingStatus.Waitlisted))
                .ToListAsync(cancellationToken);

            foreach (var booking in strandedBookings)
            {
                booking.Status = ClassBookingStatus.Cancelled;
                booking.CancelledAt = now;
            }
        }
        else
        {
            schedule.IsActive = true;

            var today = DateOnly.FromDateTime(now.UtcDateTime);
            var throughDate = today.AddDays(ClassSessionPlanner.DefaultWindowDays);

            var existingDates = await db.ClassSessions
                .Where(s => s.ClassScheduleId == schedule.Id && s.Status != ClassSessionStatus.Cancelled)
                .Select(s => DateOnly.FromDateTime(s.StartsAt.UtcDateTime))
                .ToListAsync(cancellationToken);

            var fresh = ClassSessionPlanner.BuildSessions(schedule, today, throughDate, existingDates.ToHashSet());
            db.ClassSessions.AddRange(fresh);
            schedule.GeneratedThroughDate = throughDate;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
