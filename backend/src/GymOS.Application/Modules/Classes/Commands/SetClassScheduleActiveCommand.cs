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

            var upcoming = await db.ClassSessions
                .Where(s => s.ClassScheduleId == schedule.Id && s.StartsAt > now && s.Status == ClassSessionStatus.Scheduled)
                .ToListAsync(cancellationToken);

            foreach (var session in upcoming)
            {
                session.Status = ClassSessionStatus.Cancelled;
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
