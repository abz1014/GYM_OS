using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Common.Security;
using GymOS.Application.Modules.Attendance.Dtos;
using GymOS.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Attendance.Queries;

public record GetAttendanceHistoryQuery(
    Guid? MemberId, Guid? BranchId, DateOnly? FromDate, DateOnly? ToDate, string? SearchTerm, bool? CheckedInOnly, int Page = 1, int PageSize = 20)
    : IQuery<PagedList<AttendanceRecordDto>>;

public class GetAttendanceHistoryQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetAttendanceHistoryQuery, PagedList<AttendanceRecordDto>>
{
    public async Task<PagedList<AttendanceRecordDto>> Handle(GetAttendanceHistoryQuery request, CancellationToken cancellationToken)
    {
        // Omitting BranchId used to mean "every branch" with no server-side restriction at all —
        // found live via a Receptionist seeded with access to one branch reading all 301 members.
        // Restricting to the caller's own UserBranchAccess rows here closes that regardless of
        // whether BranchId was supplied (BranchScopeBehavior already rejected a foreign one).
        var accessibleBranchIds = await BranchAccessResolver.GetAccessibleBranchIdsAsync(db, currentUser, cancellationToken);
        var query = db.AttendanceRecords.AsNoTracking().Where(a => accessibleBranchIds.Contains(a.BranchId));

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

        if (request.CheckedInOnly == true)
        {
            query = query.Where(a => a.CheckOutAt == null);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(a => a.Member!.FirstName.Contains(term) || a.Member.LastName.Contains(term));
        }

        // CheckInAt (DateTimeOffset) can't be ordered in SQL on SQLite (the in-memory test
        // provider) — pull the filtered set once and order/page it client-side, the same
        // trade-off already made for leads and at-risk members elsewhere in this codebase.
        var allMatching = await query
            .Select(a => new AttendanceRecordDto(
                a.Id, a.MemberId, a.Member!.FirstName + " " + a.Member.LastName, a.CheckInAt, a.CheckOutAt, a.Method))
            .ToListAsync(cancellationToken);

        var ordered = allMatching.OrderByDescending(a => a.CheckInAt).ToList();
        var totalCount = ordered.Count;
        var page = ordered.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();

        return new PagedList<AttendanceRecordDto>(page, request.Page, request.PageSize, totalCount);
    }
}
