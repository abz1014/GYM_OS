using GymOS.Application.Common.Extensions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Attendance.Dtos;
using GymOS.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Attendance.Queries;

public record GetAttendanceHistoryQuery(Guid? MemberId, Guid? BranchId, DateOnly? FromDate, DateOnly? ToDate, int Page = 1, int PageSize = 20)
    : IQuery<PagedList<AttendanceRecordDto>>;

public class GetAttendanceHistoryQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetAttendanceHistoryQuery, PagedList<AttendanceRecordDto>>
{
    public Task<PagedList<AttendanceRecordDto>> Handle(GetAttendanceHistoryQuery request, CancellationToken cancellationToken)
    {
        var query = db.AttendanceRecords.AsNoTracking().AsQueryable();

        if (request.MemberId is not null)
        {
            query = query.Where(a => a.MemberId == request.MemberId);
        }

        if (request.BranchId is not null)
        {
            query = query.Where(a => a.BranchId == request.BranchId);
        }

        if (request.FromDate is not null)
        {
            var from = request.FromDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(a => a.CheckInAt >= from);
        }

        if (request.ToDate is not null)
        {
            var to = request.ToDate.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            query = query.Where(a => a.CheckInAt <= to);
        }

        var projected = query
            .OrderByDescending(a => a.CheckInAt)
            .Select(a => new AttendanceRecordDto(
                a.Id, a.MemberId, a.Member!.FirstName + " " + a.Member.LastName, a.CheckInAt, a.CheckOutAt, a.Method));

        return projected.ToPagedListAsync(request.Page, request.PageSize, cancellationToken);
    }
}
