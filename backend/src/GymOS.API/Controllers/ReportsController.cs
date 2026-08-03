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
}
