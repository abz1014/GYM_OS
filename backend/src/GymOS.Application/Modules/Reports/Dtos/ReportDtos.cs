namespace GymOS.Application.Modules.Reports.Dtos;

public record RevenueReportPointDto(string Period, decimal Revenue);

public record AttendanceReportPointDto(DateOnly Date, int CheckIns);

public record MembershipBreakdownDto(
    IReadOnlyDictionary<string, int> ByStatus,
    IReadOnlyDictionary<string, int> ByPlanType);
