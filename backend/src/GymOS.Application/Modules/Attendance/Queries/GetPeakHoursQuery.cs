using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Common.Security;
using GymOS.Application.Modules.Attendance.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Attendance.Queries;

public record GetPeakHoursQuery(Guid? BranchId, DateOnly FromDate, DateOnly ToDate) : IQuery<List<PeakHourBucketDto>>;

public class GetPeakHoursQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetPeakHoursQuery, List<PeakHourBucketDto>>
{
    public async Task<List<PeakHourBucketDto>> Handle(GetPeakHoursQuery request, CancellationToken cancellationToken)
    {
        var from = request.FromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var to = request.ToDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var accessibleBranchIds = await BranchAccessResolver.GetAccessibleBranchIdsAsync(db, currentUser, cancellationToken);
        var query = db.AttendanceRecords.AsNoTracking()
            .Where(a => a.CheckInAt >= from && a.CheckInAt <= to && accessibleBranchIds.Contains(a.BranchId));

        if (request.BranchId is not null)
        {
            query = query.Where(a => a.BranchId == request.BranchId);
        }

        var buckets = await query
            .GroupBy(a => a.CheckInAt.Hour)
            .Select(g => new PeakHourBucketDto(g.Key, g.Count()))
            .ToListAsync(cancellationToken);

        return Enumerable.Range(0, 24)
            .Select(hour => buckets.FirstOrDefault(b => b.HourOfDay == hour) ?? new PeakHourBucketDto(hour, 0))
            .ToList();
    }
}
