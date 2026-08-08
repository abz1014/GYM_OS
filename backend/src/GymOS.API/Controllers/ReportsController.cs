using GymOS.API.Authorization;
using GymOS.Application.Modules.Reports.Dtos;
using GymOS.Application.Modules.Reports.Queries;
using GymOS.Shared;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymOS.API.Controllers;

[ApiController]
[Authorize]
[Route("api/reports")]
public class ReportsController(ISender mediator) : ControllerBase
{
    [HttpGet("revenue")]
    [RequirePermission(PermissionCodes.Reports.View)]
    public async Task<ActionResult<List<RevenueReportPointDto>>> Revenue(int monthsBack = 6, CancellationToken cancellationToken = default)
        => Ok(await mediator.Send(new GetRevenueReportQuery(monthsBack), cancellationToken));

    [HttpGet("revenue/export")]
    [RequirePermission(PermissionCodes.Reports.View)]
    public async Task<IActionResult> ExportRevenue(int monthsBack = 6, CancellationToken cancellationToken = default)
    {
        var bytes = await mediator.Send(new ExportRevenueReportQuery(monthsBack), cancellationToken);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "revenue-report.xlsx");
    }

    [HttpGet("attendance")]
    [RequirePermission(PermissionCodes.Reports.View)]
    public async Task<ActionResult<List<AttendanceReportPointDto>>> Attendance(int daysBack = 30, CancellationToken cancellationToken = default)
        => Ok(await mediator.Send(new GetAttendanceReportQuery(daysBack), cancellationToken));

    [HttpGet("attendance/export")]
    [RequirePermission(PermissionCodes.Reports.View)]
    public async Task<IActionResult> ExportAttendance(int daysBack = 30, CancellationToken cancellationToken = default)
    {
        var bytes = await mediator.Send(new ExportAttendanceReportQuery(daysBack), cancellationToken);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "attendance-report.xlsx");
    }

    [HttpGet("membership-breakdown")]
    [RequirePermission(PermissionCodes.Reports.View)]
    public async Task<ActionResult<MembershipBreakdownDto>> MembershipBreakdown(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetMembershipBreakdownQuery(), cancellationToken));

    [HttpGet("membership-breakdown/export")]
    [RequirePermission(PermissionCodes.Reports.View)]
    public async Task<IActionResult> ExportMembershipBreakdown(CancellationToken cancellationToken)
    {
        var bytes = await mediator.Send(new ExportMembershipReportQuery(), cancellationToken);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "membership-report.xlsx");
    }

    /// <summary>Share of gym visits that produced a recorded workout — see GetLoggingCaptureReportQuery.</summary>
    [HttpGet("logging-capture")]
    [RequirePermission(PermissionCodes.Reports.View)]
    public async Task<ActionResult<LoggingCaptureReportDto>> LoggingCapture(int weeksBack = 12, CancellationToken cancellationToken = default)
        => Ok(await mediator.Send(new GetLoggingCaptureReportQuery(weeksBack), cancellationToken));

    /// <summary>Share of members who came back in week 2 / 4 / 12 — see GetReturnRateReportQuery.</summary>
    [HttpGet("return-rate")]
    [RequirePermission(PermissionCodes.Reports.View)]
    public async Task<ActionResult<ReturnRateReportDto>> ReturnRate(CancellationToken cancellationToken = default)
        => Ok(await mediator.Send(new GetReturnRateReportQuery(), cancellationToken));

    [HttpGet("trainer-commissions")]
    [RequirePermission(PermissionCodes.Reports.View)]
    public async Task<ActionResult<List<TrainerCommissionReportRowDto>>> TrainerCommissions(int monthsBack = 6, CancellationToken cancellationToken = default)
        => Ok(await mediator.Send(new GetTrainerCommissionReportQuery(monthsBack), cancellationToken));

    [HttpGet("trainer-commissions/export")]
    [RequirePermission(PermissionCodes.Reports.View)]
    public async Task<IActionResult> ExportTrainerCommissions(int monthsBack = 6, CancellationToken cancellationToken = default)
    {
        var bytes = await mediator.Send(new ExportTrainerCommissionReportQuery(monthsBack), cancellationToken);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "trainer-commission-report.xlsx");
    }

    [HttpGet("equipment-downtime")]
    [RequirePermission(PermissionCodes.Reports.View)]
    public async Task<ActionResult<List<EquipmentDowntimeReportRowDto>>> EquipmentDowntime(int monthsBack = 6, CancellationToken cancellationToken = default)
        => Ok(await mediator.Send(new GetEquipmentDowntimeReportQuery(monthsBack), cancellationToken));

    [HttpGet("equipment-downtime/export")]
    [RequirePermission(PermissionCodes.Reports.View)]
    public async Task<IActionResult> ExportEquipmentDowntime(int monthsBack = 6, CancellationToken cancellationToken = default)
    {
        var bytes = await mediator.Send(new ExportEquipmentDowntimeReportQuery(monthsBack), cancellationToken);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "equipment-downtime-report.xlsx");
    }

    [HttpGet("inventory-stock-movement")]
    [RequirePermission(PermissionCodes.Reports.View)]
    public async Task<ActionResult<List<InventoryStockMovementReportRowDto>>> InventoryStockMovement(int daysBack = 30, CancellationToken cancellationToken = default)
        => Ok(await mediator.Send(new GetInventoryStockMovementReportQuery(daysBack), cancellationToken));

    [HttpGet("inventory-stock-movement/export")]
    [RequirePermission(PermissionCodes.Reports.View)]
    public async Task<IActionResult> ExportInventoryStockMovement(int daysBack = 30, CancellationToken cancellationToken = default)
    {
        var bytes = await mediator.Send(new ExportInventoryStockMovementReportQuery(daysBack), cancellationToken);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "inventory-stock-movement-report.xlsx");
    }

    [HttpGet("crm-pipeline")]
    [RequirePermission(PermissionCodes.Reports.View)]
    public async Task<ActionResult<CrmPipelineConversionReportDto>> CrmPipeline(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetCrmPipelineConversionReportQuery(), cancellationToken));

    [HttpGet("crm-pipeline/export")]
    [RequirePermission(PermissionCodes.Reports.View)]
    public async Task<IActionResult> ExportCrmPipeline(CancellationToken cancellationToken)
    {
        var bytes = await mediator.Send(new ExportCrmPipelineConversionReportQuery(), cancellationToken);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "crm-pipeline-report.xlsx");
    }

    [HttpGet("workout-activity")]
    [RequirePermission(PermissionCodes.Reports.View)]
    public async Task<ActionResult<List<WorkoutActivityReportRowDto>>> WorkoutActivity(int daysBack = 30, CancellationToken cancellationToken = default)
        => Ok(await mediator.Send(new GetWorkoutActivityReportQuery(daysBack), cancellationToken));

    [HttpGet("workout-activity/export")]
    [RequirePermission(PermissionCodes.Reports.View)]
    public async Task<IActionResult> ExportWorkoutActivity(int daysBack = 30, CancellationToken cancellationToken = default)
    {
        var bytes = await mediator.Send(new ExportWorkoutActivityReportQuery(daysBack), cancellationToken);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "workout-activity-report.xlsx");
    }

    [HttpGet("nutrition")]
    [RequirePermission(PermissionCodes.Reports.View)]
    public async Task<ActionResult<NutritionReportDto>> Nutrition(int daysBack = 30, CancellationToken cancellationToken = default)
        => Ok(await mediator.Send(new GetNutritionReportQuery(daysBack), cancellationToken));

    [HttpGet("nutrition/export")]
    [RequirePermission(PermissionCodes.Reports.View)]
    public async Task<IActionResult> ExportNutrition(int daysBack = 30, CancellationToken cancellationToken = default)
    {
        var bytes = await mediator.Send(new ExportNutritionReportQuery(daysBack), cancellationToken);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "nutrition-report.xlsx");
    }

    [HttpGet("at-risk-members")]
    [RequirePermission(PermissionCodes.Reports.View)]
    public async Task<ActionResult<List<AtRiskMemberRowDto>>> AtRiskMembers(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetAtRiskMembersReportQuery(), cancellationToken));

    [HttpGet("at-risk-members/export")]
    [RequirePermission(PermissionCodes.Reports.View)]
    public async Task<IActionResult> ExportAtRiskMembers(CancellationToken cancellationToken)
    {
        var bytes = await mediator.Send(new ExportAtRiskMembersReportQuery(), cancellationToken);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "at-risk-members-report.xlsx");
    }

    [HttpGet("cohort-retention")]
    [RequirePermission(PermissionCodes.Reports.View)]
    public async Task<ActionResult<List<CohortRetentionPointDto>>> CohortRetention(int monthsBack = 12, CancellationToken cancellationToken = default)
        => Ok(await mediator.Send(new GetCohortRetentionReportQuery(monthsBack), cancellationToken));

    [HttpGet("cohort-retention/export")]
    [RequirePermission(PermissionCodes.Reports.View)]
    public async Task<IActionResult> ExportCohortRetention(int monthsBack = 12, CancellationToken cancellationToken = default)
    {
        var bytes = await mediator.Send(new ExportCohortRetentionReportQuery(monthsBack), cancellationToken);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "cohort-retention-report.xlsx");
    }

    [HttpGet("ltv-by-source")]
    [RequirePermission(PermissionCodes.Reports.View)]
    public async Task<ActionResult<List<LtvBySourceRowDto>>> LtvBySource(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetLtvByAcquisitionSourceQuery(), cancellationToken));

    [HttpGet("ltv-by-source/export")]
    [RequirePermission(PermissionCodes.Reports.View)]
    public async Task<IActionResult> ExportLtvBySource(CancellationToken cancellationToken)
    {
        var bytes = await mediator.Send(new ExportLtvByAcquisitionSourceQuery(), cancellationToken);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "ltv-by-source-report.xlsx");
    }
}
