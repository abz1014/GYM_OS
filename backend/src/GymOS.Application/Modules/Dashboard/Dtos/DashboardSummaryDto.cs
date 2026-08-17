namespace GymOS.Application.Modules.Dashboard.Dtos;

public record DashboardSummaryDto(
    // Null — not zero — when the caller may not see money. dashboard.view is held by every staff
    // role, including three with no reason to read the gym's takings, so the two figures below are
    // gated separately on billing.view in the handler. Zero would be a number a caller could act on,
    // and on a quiet morning it is indistinguishable from a real one.
    decimal? TodayRevenue,
    decimal? TodayCashCollected,
    // Same nullability, same gate, for the same reason as the two above.
    //
    // The dashboard reported only today's takings, so an owner opening the app at 9am was judging
    // the morning rather than the month. Both figures are completed payments MINUS completed
    // refunds — the arithmetic InvoiceStatusPolicy and GetInvoicesQuery already use, because a
    // third reading of "revenue" is exactly the defect the invoice list/detail disagreement was.
    //
    // Last month is the SAME SPAN of the previous month, not the whole of it: comparing a 17th-of
    // -the-month figure against a full previous month reads as a collapse in revenue every month
    // until roughly the 28th, which is a lie told by the comparison rather than the data.
    //
    // Deliberately NOT here: MRR. Nothing in this system records contracted recurring revenue —
    // MemberMembership carries a price and an auto-renew flag, not a commitment — so any figure
    // would be a guess dressed as an accounting number, and this codebase does not render numbers
    // that are not backed by real data. If MRR is wanted it needs a real source first.
    decimal? RevenueThisMonth,
    decimal? RevenueLastMonth,
    int ActiveMembersCount,
    int NewMembersThisMonthCount,
    int ExpiringMembershipsNext7DaysCount,
    int TodayAttendanceCount,
    // Wave 2/3 metrics default to 0 until Trainers/Equipment/Maintenance/Inventory ship —
    // the widgets render, just with a "coming soon" empty state instead of real data.
    int TrainerScheduleTodayCount,
    int EquipmentAlertsCount,
    int MaintenanceRemindersCount,
    int InventoryAlertsCount);
