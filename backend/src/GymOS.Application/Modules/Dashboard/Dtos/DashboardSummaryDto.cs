namespace GymOS.Application.Modules.Dashboard.Dtos;

public record DashboardSummaryDto(
    decimal TodayRevenue,
    decimal TodayCashCollected,
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
