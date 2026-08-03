namespace GymOS.Application.Modules.Reports.Dtos;

public record RevenueReportPointDto(string Period, decimal Revenue);

public record AttendanceReportPointDto(DateOnly Date, int CheckIns);

public record MembershipBreakdownDto(
    IReadOnlyDictionary<string, int> ByStatus,
    IReadOnlyDictionary<string, int> ByPlanType);

public record TrainerCommissionReportRowDto(string TrainerName, decimal TotalPending, decimal TotalPaid, int RecordCount);

public record EquipmentDowntimeReportRowDto(
    string AssetName, string AssetTag, int Incidents, double TotalDowntimeHours, decimal TotalMaintenanceCost);

public record InventoryStockMovementReportRowDto(
    string ItemName, string Sku, int TotalIn, int TotalOut, int NetChange, int CurrentQuantityOnHand);

public record CrmPipelineConversionReportDto(
    IReadOnlyDictionary<string, int> ByStage, int TotalLeads, int ConvertedCount, decimal ConversionRatePercent);
