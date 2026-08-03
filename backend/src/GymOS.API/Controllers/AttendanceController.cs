using GymOS.API.Authorization;
using GymOS.Application.Modules.Attendance.Commands;
using GymOS.Application.Modules.Attendance.Dtos;
using GymOS.Application.Modules.Attendance.Queries;
using GymOS.Shared;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymOS.API.Controllers;

[ApiController]
[Authorize]
[Route("api/attendance")]
public class AttendanceController(ISender mediator) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.Attendance.View)]
    public async Task<ActionResult<PagedList<AttendanceRecordDto>>> List(
        [FromQuery] Guid? memberId, [FromQuery] Guid? branchId, [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        => Ok(await mediator.Send(new GetAttendanceHistoryQuery(memberId, branchId, fromDate, toDate, page, pageSize), cancellationToken));

    [HttpGet("peak-hours")]
    [RequirePermission(PermissionCodes.Attendance.View)]
    public async Task<ActionResult<List<PeakHourBucketDto>>> PeakHours(
        [FromQuery] Guid? branchId, [FromQuery] DateOnly fromDate, [FromQuery] DateOnly toDate, CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetPeakHoursQuery(branchId, fromDate, toDate), cancellationToken));

    [HttpPost("check-in")]
    [RequirePermission(PermissionCodes.Attendance.CheckIn)]
    public async Task<ActionResult<Guid>> CheckIn(CheckInCommand command, CancellationToken cancellationToken)
        => Ok(await mediator.Send(command, cancellationToken));

    [HttpPost("{id:guid}/check-out")]
    [RequirePermission(PermissionCodes.Attendance.CheckIn)]
    public async Task<IActionResult> CheckOut(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new CheckOutCommand(id), cancellationToken);
        return NoContent();
    }
}
