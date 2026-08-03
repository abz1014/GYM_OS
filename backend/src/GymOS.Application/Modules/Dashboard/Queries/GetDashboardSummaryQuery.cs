using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Dashboard.Dtos;
using GymOS.Domain.Billing;
using GymOS.Domain.Members;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Dashboard.Queries;

public record GetDashboardSummaryQuery(Guid? BranchId) : IQuery<DashboardSummaryDto>;

public class GetDashboardSummaryQueryHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    public async Task<DashboardSummaryDto> Handle(GetDashboardSummaryQuery request, CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.UtcNow;
        var todayStart = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset);
        var todayEnd = todayStart.AddDays(1);
        var monthStart = new DateOnly(now.Year, now.Month, 1);
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var sevenDaysOut = today.AddDays(7);

        var payments = db.Payments.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Completed && p.PaidAt >= todayStart && p.PaidAt < todayEnd);

        if (request.BranchId is not null)
        {
            payments = payments.Where(p => p.Invoice!.BranchId == request.BranchId);
        }

        var todayRevenue = await payments.SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;
        var todayCash = await payments.Where(p => p.Method == PaymentMethod.Cash)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

        var members = db.Members.AsNoTracking().AsQueryable();
        if (request.BranchId is not null)
        {
            members = members.Where(m => m.BranchId == request.BranchId);
        }

        var activeMembersCount = await members.CountAsync(m => m.Status == MemberStatus.Active, cancellationToken);
        var newMembersThisMonth = await members.CountAsync(m => m.JoinDate >= monthStart, cancellationToken);

        var expiringMemberships = db.MemberMemberships.AsNoTracking()
            .Where(mm => mm.EndDate >= today && mm.EndDate <= sevenDaysOut && mm.Status == MemberMembershipStatus.Active);

        if (request.BranchId is not null)
        {
            expiringMemberships = expiringMemberships.Where(mm => mm.Member!.BranchId == request.BranchId);
        }

        var expiringCount = await expiringMemberships.CountAsync(cancellationToken);

        var attendanceToday = db.AttendanceRecords.AsNoTracking()
            .Where(a => a.CheckInAt >= todayStart && a.CheckInAt < todayEnd);

        if (request.BranchId is not null)
        {
            attendanceToday = attendanceToday.Where(a => a.BranchId == request.BranchId);
        }

        var todayAttendanceCount = await attendanceToday.CountAsync(cancellationToken);

        return new DashboardSummaryDto(
            todayRevenue, todayCash, activeMembersCount, newMembersThisMonth, expiringCount, todayAttendanceCount,
            TrainerScheduleTodayCount: 0, EquipmentAlertsCount: 0, MaintenanceRemindersCount: 0, InventoryAlertsCount: 0);
    }
}
