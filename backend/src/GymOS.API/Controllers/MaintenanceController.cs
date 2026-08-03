using GymOS.API.Authorization;
using GymOS.Application.Modules.Maintenance.Commands;
using GymOS.Application.Modules.Maintenance.Dtos;
using GymOS.Application.Modules.Maintenance.Queries;
using GymOS.Domain.Maintenance;
using GymOS.Shared;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymOS.API.Controllers;

[ApiController]
[Authorize]
[Route("api/work-orders")]
public class MaintenanceController(ISender mediator) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.Maintenance.View)]
    public async Task<ActionResult<List<WorkOrderListItemDto>>> List(
        [FromQuery] Guid? branchId, [FromQuery] WorkOrderStatus? status, CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetWorkOrdersListQuery(branchId, status), cancellationToken));

    [HttpGet("schedules")]
    [RequirePermission(PermissionCodes.Maintenance.View)]
    public async Task<ActionResult<List<MaintenanceScheduleDto>>> Schedules([FromQuery] Guid? branchId, CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetMaintenanceSchedulesListQuery(branchId), cancellationToken));

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCodes.Maintenance.View)]
    public async Task<ActionResult<WorkOrderDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetWorkOrderByIdQuery(id), cancellationToken));

    [HttpPost]
    [RequirePermission(PermissionCodes.Maintenance.Manage)]
    public async Task<ActionResult<Guid>> Create(CreateWorkOrderCommand command, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPost("schedules")]
    [RequirePermission(PermissionCodes.Maintenance.Manage)]
    public async Task<ActionResult<Guid>> CreateSchedule(CreateMaintenanceScheduleCommand command, CancellationToken cancellationToken)
        => Ok(await mediator.Send(command, cancellationToken));

    [HttpPut("{id:guid}/status")]
    [RequirePermission(PermissionCodes.Maintenance.Manage)]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateWorkOrderStatusCommand command, CancellationToken cancellationToken)
    {
        await mediator.Send(command with { WorkOrderId = id }, cancellationToken);
        return NoContent();
    }
}
