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
        decimal? revenueThisMonth = null;
        decimal? revenueLastMonth = null;

        if (currentUser.HasPermission(PermissionCodes.Billing.View))
        {
            /*
             * Month-to-date, and the SAME SPAN of the month before.
             *
             * Both windows run from the first of the month through the end of today, so a figure
             * taken on the 17th is compared against seventeen days rather than thirty-one. Compared
             * against a whole previous month, a healthy gym would show revenue collapsing every day
             * of every month until roughly the 28th and then recovering — a lie manufactured by the
             * comparison rather than read from the data, and a dashboard headline is exactly where
             * it would be believed.
             *
             * The previous window is clamped at this month's start so a 31st-of-March reading cannot
             * run February's window forward into March and count March's money on both sides.
             */
            var monthStartTs = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, now.Offset);
            var previousMonthStartTs = monthStartTs.AddMonths(-1);
            var previousMonthEndTs = previousMonthStartTs.AddDays(now.Day);
            if (previousMonthEndTs > monthStartTs)
            {
                previousMonthEndTs = monthStartTs;
            }

            var payments = db.Payments.AsNoTracking()
                .Where(p => p.Status == PaymentStatus.Completed && accessibleBranchIds.Contains(p.Invoice!.BranchId));

            if (request.BranchId is not null)
            {
                payments = payments.Where(p => p.Invoice!.BranchId == request.BranchId);
            }

            /*
             * Branch and status filter in SQL; every date window below is applied in memory.
             *
             * PaidAt is a DateTimeOffset, and SQLite — the in-memory test provider — cannot
             * range-filter one alongside a parameterised .Contains(), the same constraint every
             * DateTimeOffset window in this codebase works around. This is not a stylistic
             * preference: the previous in-SQL version of the today filter meant this whole handler
             * threw on the test harness, which is why the dashboard had no Application tests at all
             * and why its money arithmetic went unpinned. Only (amount, method, timestamp) is
             * pulled, already narrowed to the branches the caller may see, and one materialisation
             * answers all four figures instead of costing four queries.
             */
            var paymentRows = await payments
                .Select(p => new PaymentEvent(p.Amount, p.Method, p.PaidAt))
                .ToListAsync(cancellationToken);

            // Today stays GROSS, deliberately unchanged: "cash collected today" is what went into
            // the drawer, and it is reconciled against the drawer. The month figures below are the
            // accounting question, and they net.
            todayRevenue = paymentRows.Where(p => p.PaidAt >= todayStart && p.PaidAt < todayEnd).Sum(p => p.Amount);
            todayCash = paymentRows
                .Where(p => p.Method == PaymentMethod.Cash && p.PaidAt >= todayStart && p.PaidAt < todayEnd)
                .Sum(p => p.Amount);

            /*
             * Completed payments MINUS completed refunds, matching InvoiceStatusPolicy,
             * GetInvoicesQuery and RecurringBillingJob. A gym that took $500 and gave $400 of it
             * back did not make $500, and this codebase has already been bitten once by two screens
             * reading "revenue" differently — the invoice list summed payments and stopped there
             * while the detail subtracted refunds, so the same invoice reported different money
             * depending on which screen you came from. A third reading is not on offer.
             *
             * Refunds are dated by RefundedAt, not by the payment they reverse: a refund issued this
             * month against last month's payment reduces THIS month, which is when the money left.
             */
            var refunds = db.Refunds.AsNoTracking()
                .Where(r => r.Status == RefundStatus.Completed
                    && accessibleBranchIds.Contains(r.Payment!.Invoice!.BranchId));

            if (request.BranchId is not null)
            {
                refunds = refunds.Where(r => r.Payment!.Invoice!.BranchId == request.BranchId);
            }

            var refundRows = await refunds
                .Select(r => new { r.Amount, r.RefundedAt })
                .ToListAsync(cancellationToken);

            decimal SumBetween(DateTimeOffset from, DateTimeOffset to) =>
                paymentRows.Where(p => p.PaidAt >= from && p.PaidAt < to).Sum(p => p.Amount)
                - refundRows.Where(r => r.RefundedAt >= from && r.RefundedAt < to).Sum(r => r.Amount);

            revenueThisMonth = SumBetween(monthStartTs, todayEnd);
            revenueLastMonth = SumBetween(previousMonthStartTs, previousMonthEndTs);
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
            .Where(a => accessibleBranchIds.Contains(a.BranchId));

        if (request.BranchId is not null)
        {
            attendanceToday = attendanceToday.Where(a => a.BranchId == request.BranchId);
        }

        // Same reduction, same reason as the payment windows above: CheckInAt is a DateTimeOffset,
        // which SQLite cannot range-filter alongside the branch .Contains(), so the day is cut in
        // memory. Every other query over CheckInAt in this codebase already does this.
        var todayAttendanceCount = (await attendanceToday.Select(a => a.CheckInAt).ToListAsync(cancellationToken))
            .Count(checkInAt => checkInAt >= todayStart && checkInAt < todayEnd);

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
            todayRevenue, todayCash, revenueThisMonth, revenueLastMonth,
            activeMembersCount, newMembersThisMonth, expiringCount, todayAttendanceCount,
            trainerScheduleTodayCount, equipmentAlertsCount, maintenanceRemindersCount, inventoryAlertsCount);
    }

    /// <summary>A payment reduced to the three things every money figure on this dashboard needs,
    /// so today and both month windows share one materialisation.</summary>
    private sealed record PaymentEvent(decimal Amount, PaymentMethod Method, DateTimeOffset PaidAt);
}
