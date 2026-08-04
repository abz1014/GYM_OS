using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Common.Security;
using GymOS.Application.Modules.Classes.Dtos;
using GymOS.Domain.Classes;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Classes.Queries;

/// <summary>
/// Upcoming (or date-ranged) concrete sessions — the staff calendar view, and the row set that
/// bookings attach to in Step 2. Defaults to the next two weeks (the booking window) when no range
/// is given, and hides cancelled sessions unless explicitly asked for.
/// </summary>
public record GetClassSessionsListQuery(Guid? BranchId, DateOnly? FromDate, DateOnly? ToDate, bool IncludeCancelled = false)
    : IQuery<List<ClassSessionDto>>;

public class GetClassSessionsListQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetClassSessionsListQuery, List<ClassSessionDto>>
{
    public async Task<List<ClassSessionDto>> Handle(GetClassSessionsListQuery request, CancellationToken cancellationToken)
    {
        var accessibleBranchIds = await BranchAccessResolver.GetAccessibleBranchIdsAsync(db, currentUser, cancellationToken);

        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);
        // Bounds must be UTC-offset DateTimeOffsets: StartsAt is a 'timestamp with time zone' and
        // Npgsql rejects any non-UTC offset. Building these as plain DateTime would let EF attach the
        // server's local offset and blow up at execution time.
        var from = new DateTimeOffset((request.FromDate ?? today).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var to = new DateTimeOffset(
            (request.ToDate ?? today.AddDays(ClassSessionPlanner.DefaultWindowDays)).ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);

        var query = db.ClassSessions.AsNoTracking()
            .Include(s => s.ClassType)
            .Include(s => s.Trainer!).ThenInclude(t => t.User)
            .Where(s => accessibleBranchIds.Contains(s.BranchId))
            .Where(s => s.StartsAt >= from && s.StartsAt <= to);

        if (request.BranchId is not null)
        {
            query = query.Where(s => s.BranchId == request.BranchId);
        }

        if (!request.IncludeCancelled)
        {
            query = query.Where(s => s.Status != ClassSessionStatus.Cancelled);
        }

        return await query
            .OrderBy(s => s.StartsAt)
            .Select(s => new ClassSessionDto(
                s.Id, s.ClassScheduleId, s.ClassTypeId, s.ClassType!.Name, s.ClassType.ColorHex,
                s.TrainerId, s.Trainer == null ? null : s.Trainer.User!.FirstName + " " + s.Trainer.User.LastName,
                s.StartsAt, s.DurationMinutes, s.Capacity, s.Location, s.Status,
                s.Bookings.Count(b => b.Status == ClassBookingStatus.Booked || b.Status == ClassBookingStatus.CheckedIn),
                s.Bookings.Count(b => b.Status == ClassBookingStatus.Waitlisted)))
            .ToListAsync(cancellationToken);
    }
}
