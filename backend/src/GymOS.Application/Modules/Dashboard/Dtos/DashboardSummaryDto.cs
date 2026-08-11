namespace GymOS.Application.Modules.Dashboard.Dtos;

public record DashboardSummaryDto(
    // Null — not zero — when the caller may not see money. dashboard.view is held by every staff
    // role, including three with no reason to read the gym's takings, so the two figures below are
    // gated separately on billing.view in the handler. Zero would be a number a caller could act on,
    // and on a quiet morning it is indistinguishable from a real one.
    decimal? TodayRevenue,
    decimal? TodayCashCollected,
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
