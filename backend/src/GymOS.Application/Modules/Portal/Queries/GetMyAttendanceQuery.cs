using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Attendance.Dtos;
using GymOS.Application.Modules.Attendance.Queries;
using GymOS.Shared;
using MediatR;

namespace GymOS.Application.Modules.Portal.Queries;

public record GetMyAttendanceQuery(DateOnly? FromDate, DateOnly? ToDate, int Page = 1, int PageSize = 20)
    : IQuery<PagedList<AttendanceRecordDto>>;

public class GetMyAttendanceQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser, ISender sender)
    : IRequestHandler<GetMyAttendanceQuery, PagedList<AttendanceRecordDto>>
{
    public async Task<PagedList<AttendanceRecordDto>> Handle(GetMyAttendanceQuery request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);

        return await sender.Send(
            new GetAttendanceHistoryQuery(memberId, BranchId: null, request.FromDate, request.ToDate, request.Page, request.PageSize),
            cancellationToken);
    }
}
