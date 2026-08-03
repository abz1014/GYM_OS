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
}
