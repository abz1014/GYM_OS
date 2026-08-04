using GymOS.API.Authorization;
using GymOS.Application.Modules.Classes.Commands;
using GymOS.Application.Modules.Classes.Dtos;
using GymOS.Application.Modules.Classes.Queries;
using GymOS.Shared;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymOS.API.Controllers;

[ApiController]
[Authorize]
[Route("api/classes")]
public class ClassesController(ISender mediator) : ControllerBase
{
    [HttpGet("types")]
    [RequirePermission(PermissionCodes.Classes.View)]
    public async Task<ActionResult<List<ClassTypeDto>>> Types(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetClassTypesListQuery(), cancellationToken));

    [HttpPost("types")]
    [RequirePermission(PermissionCodes.Classes.Manage)]
    public async Task<ActionResult<Guid>> CreateType(CreateClassTypeCommand command, CancellationToken cancellationToken)
        => Ok(await mediator.Send(command, cancellationToken));

    [HttpGet("schedules")]
    [RequirePermission(PermissionCodes.Classes.View)]
    public async Task<ActionResult<List<ClassScheduleDto>>> Schedules([FromQuery] Guid? branchId, CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetClassSchedulesListQuery(branchId), cancellationToken));

    [HttpPost("schedules")]
    [RequirePermission(PermissionCodes.Classes.Manage)]
    public async Task<ActionResult<Guid>> CreateSchedule(CreateClassScheduleCommand command, CancellationToken cancellationToken)
        => Ok(await mediator.Send(command, cancellationToken));

    [HttpPut("schedules/{id:guid}/active")]
    [RequirePermission(PermissionCodes.Classes.Manage)]
    public async Task<IActionResult> SetScheduleActive(Guid id, SetClassScheduleActiveCommand command, CancellationToken cancellationToken)
    {
        await mediator.Send(command with { ClassScheduleId = id }, cancellationToken);
        return NoContent();
    }

    [HttpGet("sessions")]
    [RequirePermission(PermissionCodes.Classes.View)]
    public async Task<ActionResult<List<ClassSessionDto>>> Sessions(
        [FromQuery] Guid? branchId, [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate,
        [FromQuery] bool includeCancelled = false, CancellationToken cancellationToken = default)
        => Ok(await mediator.Send(new GetClassSessionsListQuery(branchId, fromDate, toDate, includeCancelled), cancellationToken));

    [HttpPost("sessions/{id:guid}/cancel")]
    [RequirePermission(PermissionCodes.Classes.Manage)]
    public async Task<IActionResult> CancelSession(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new CancelClassSessionCommand(id), cancellationToken);
        return NoContent();
    }
}
