using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Portal.Dtos;
using GymOS.Domain.Classes;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Portal.Queries;

/// <summary>
/// The classes a member can book — upcoming Scheduled sessions at their own branch, each annotated
/// with this member's own current booking status (null if they aren't on it) so the UI can show
/// Book / Join waitlist / Booked / Waitlisted. Branch and identity both come from the member's own
/// row via MyMemberResolver — never a caller-supplied id.
/// </summary>
public record GetMyClassScheduleQuery : IQuery<List<MyClassSessionDto>>;

public class GetMyClassScheduleQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetMyClassScheduleQuery, List<MyClassSessionDto>>
{
    public async Task<List<MyClassSessionDto>> Handle(GetMyClassScheduleQuery request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);
        var branchId = await db.Members.AsNoTracking().Where(m => m.Id == memberId).Select(m => m.BranchId).FirstAsync(cancellationToken);

        var now = dateTimeProvider.UtcNow;

        return await db.ClassSessions.AsNoTracking()
            .Include(s => s.ClassType)
            .Include(s => s.Trainer!).ThenInclude(t => t.User)
            .Where(s => s.BranchId == branchId && s.Status == ClassSessionStatus.Scheduled && s.StartsAt >= now)
            .OrderBy(s => s.StartsAt)
            .Select(s => new MyClassSessionDto(
                s.Id,
                s.ClassType!.Name,
                s.ClassType.ColorHex,
                s.Trainer == null ? null : s.Trainer.User!.FirstName + " " + s.Trainer.User.LastName,
                s.StartsAt,
                s.DurationMinutes,
                s.Capacity,
                s.Location,
                s.Bookings.Count(b => b.Status == ClassBookingStatus.Booked || b.Status == ClassBookingStatus.CheckedIn),
                s.Bookings.Count(b => b.Status == ClassBookingStatus.Booked || b.Status == ClassBookingStatus.CheckedIn) >= s.Capacity,
                // This member's own live booking on the session (booked/waitlisted/checked-in), if any.
                s.Bookings
                    .Where(b => b.MemberId == memberId && (b.Status == ClassBookingStatus.Booked || b.Status == ClassBookingStatus.Waitlisted || b.Status == ClassBookingStatus.CheckedIn))
                    .Select(b => (ClassBookingStatus?)b.Status)
                    .FirstOrDefault(),
                s.Bookings
                    .Where(b => b.MemberId == memberId && (b.Status == ClassBookingStatus.Booked || b.Status == ClassBookingStatus.Waitlisted || b.Status == ClassBookingStatus.CheckedIn))
                    .Select(b => (Guid?)b.Id)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }
}
