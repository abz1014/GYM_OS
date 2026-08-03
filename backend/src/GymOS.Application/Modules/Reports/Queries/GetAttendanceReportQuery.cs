using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Reports.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Reports.Queries;

public record GetAttendanceReportQuery(int DaysBack = 30) : IQuery<List<AttendanceReportPointDto>>;

public class GetAttendanceReportQueryHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetAttendanceReportQuery, List<AttendanceReportPointDto>>
{
    public async Task<List<AttendanceReportPointDto>> Handle(GetAttendanceReportQuery request, CancellationToken cancellationToken)
        => await BuildAsync(db, dateTimeProvider, request.DaysBack, cancellationToken);

    internal static async Task<List<AttendanceReportPointDto>> BuildAsync(
        IApplicationDbContext db, IDateTimeProvider dateTimeProvider, int daysBack, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);
        var cutoff = today.AddDays(-(daysBack - 1)).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var checkIns = await db.AttendanceRecords.AsNoTracking()
            .Where(a => a.CheckInAt >= cutoff)
            .Select(a => a.CheckInAt)
            .ToListAsync(cancellationToken);

        var buckets = new List<AttendanceReportPointDto>();
        for (var i = daysBack - 1; i >= 0; i--)
        {
            var date = today.AddDays(-i);
            var count = checkIns.Count(c => DateOnly.FromDateTime(c.UtcDateTime) == date);
            buckets.Add(new AttendanceReportPointDto(date, count));
        }

        return buckets;
    }
}
