using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Common.Security;
using GymOS.Application.Modules.Dashboard.Dtos;
using GymOS.Domain.Billing;
using GymOS.Domain.Equipment;
using GymOS.Domain.Maintenance;
using GymOS.Domain.Members;
using GymOS.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Dashboard.Queries;

public record GetDashboardSummaryQuery(Guid? BranchId) : IQuery<DashboardSummaryDto>;

public class GetDashboardSummaryQueryHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider, ICurrentUserService currentUser)
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

        // Every figure below used to be tenant-wide with no branch restriction unless the caller
        // opted in to one — found live via a Receptionist seeded with access to a single branch
        // reading company-wide revenue and cash-collected figures with a plain GET, no params.
        var accessibleBranchIds = await BranchAccessResolver.GetAccessibleBranchIdsAsync(db, currentUser, cancellationToken);

        /*
         * Money is gated on billing.view, not on dashboard.view like everything else here.
         *
         * Every staff role holds dashboard.view — including Trainer, Nutritionist and Maintenance,
         * none of which has a reason to read the gym's takings. /api/reports/revenue already refuses
         * those roles, so serving the same figure from this endpoint left one door shut and one open;
         * found live with a Trainer token reading todayRevenue while its reports call returned 403.
         *
         * Skipped rather than computed-and-dropped: there is no point summing payments for a caller
         * who will not be shown them.
         */
        decimal? todayRevenue = null;
        decimal? todayCash = null;

        if (currentUser.HasPermission(PermissionCodes.Billing.View))
        {
            var payments = db.Payments.AsNoTracking()
                .Where(p => p.Status == PaymentStatus.Completed && p.PaidAt >= todayStart && p.PaidAt < todayEnd
                    && accessibleBranchIds.Contains(p.Invoice!.BranchId));

            if (request.BranchId is not null)
            {
                payments = payments.Where(p => p.Invoice!.BranchId == request.BranchId);
            }

            todayRevenue = await payments.SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;
            todayCash = await payments.Where(p => p.Method == PaymentMethod.Cash)
                .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;
        }

        var members = db.Members.AsNoTracking().Where(m => accessibleBranchIds.Contains(m.BranchId));
        if (request.BranchId is not null)
        {
            members = members.Where(m => m.BranchId == request.BranchId);
        }

        var activeMembersCount = await members.CountAsync(m => m.Status == MemberStatus.Active, cancellationToken);
        var newMembersThisMonth = await members.CountAsync(m => m.JoinDate >= monthStart, cancellationToken);

        var expiringMemberships = db.MemberMemberships.AsNoTracking()
            .Where(mm => mm.EndDate >= today && mm.EndDate <= sevenDaysOut && mm.Status == MemberMembershipStatus.Active
                && accessibleBranchIds.Contains(mm.Member!.BranchId));

        if (request.BranchId is not null)
        {
            expiringMemberships = expiringMemberships.Where(mm => mm.Member!.BranchId == request.BranchId);
        }

        var expiringCount = await expiringMemberships.CountAsync(cancellationToken);

        var attendanceToday = db.AttendanceRecords.AsNoTracking()
            .Where(a => a.CheckInAt >= todayStart && a.CheckInAt < todayEnd && accessibleBranchIds.Contains(a.BranchId));

        if (request.BranchId is not null)
        {
            attendanceToday = attendanceToday.Where(a => a.BranchId == request.BranchId);
        }

        var todayAttendanceCount = await attendanceToday.CountAsync(cancellationToken);

        var trainerSchedulesToday = db.TrainerSchedules.AsNoTracking()
            .Where(s => s.DayOfWeek == now.DayOfWeek && s.IsAvailable && s.Trainer!.IsActive
                && accessibleBranchIds.Contains(s.Trainer!.BranchId));

        if (request.BranchId is not null)
        {
            trainerSchedulesToday = trainerSchedulesToday.Where(s => s.Trainer!.BranchId == request.BranchId);
        }

        var trainerScheduleTodayCount = await trainerSchedulesToday.Select(s => s.TrainerId).Distinct().CountAsync(cancellationToken);

        var equipmentAlerts = db.Assets.AsNoTracking()
            .Where(a => (a.Status == AssetStatus.UnderMaintenance || a.Status == AssetStatus.OutOfService)
                && accessibleBranchIds.Contains(a.BranchId));

        if (request.BranchId is not null)
        {
            equipmentAlerts = equipmentAlerts.Where(a => a.BranchId == request.BranchId);
        }

        var equipmentAlertsCount = await equipmentAlerts.CountAsync(cancellationToken);

        var maintenanceReminders = db.WorkOrders.AsNoTracking()
            .Where(w => w.ScheduledDate != null && w.ScheduledDate < today
                && w.Status != WorkOrderStatus.Completed && w.Status != WorkOrderStatus.Cancelled
                && accessibleBranchIds.Contains(w.BranchId));

        if (request.BranchId is not null)
        {
            maintenanceReminders = maintenanceReminders.Where(w => w.BranchId == request.BranchId);
        }

        var maintenanceRemindersCount = await maintenanceReminders.CountAsync(cancellationToken);

        var inventoryAlerts = db.InventoryItems.AsNoTracking()
            .Where(i => i.QuantityOnHand <= i.ReorderLevel && accessibleBranchIds.Contains(i.BranchId));

        if (request.BranchId is not null)
        {
            inventoryAlerts = inventoryAlerts.Where(i => i.BranchId == request.BranchId);
        }

        var inventoryAlertsCount = await inventoryAlerts.CountAsync(cancellationToken);

        return new DashboardSummaryDto(
            todayRevenue, todayCash, activeMembersCount, newMembersThisMonth, expiringCount, todayAttendanceCount,
            trainerScheduleTodayCount, equipmentAlertsCount, maintenanceRemindersCount, inventoryAlertsCount);
    }
}
