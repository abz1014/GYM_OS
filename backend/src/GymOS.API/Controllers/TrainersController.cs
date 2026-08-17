using GymOS.API.Authorization;
using GymOS.Application.Modules.Trainers.Commands;
using GymOS.Application.Modules.Trainers.Dtos;
using GymOS.Application.Modules.Trainers.Queries;
using GymOS.Shared;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymOS.API.Controllers;

[ApiController]
[Authorize]
[Route("api/trainers")]
public class TrainersController(ISender mediator) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.Trainers.View)]
    public async Task<ActionResult<List<TrainerListItemDto>>> List([FromQuery] Guid? branchId, CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetTrainersListQuery(branchId), cancellationToken));

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCodes.Trainers.View)]
    public async Task<ActionResult<TrainerDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetTrainerByIdQuery(id), cancellationToken));

    [HttpPost]
    [RequirePermission(PermissionCodes.Trainers.Manage)]
    public async Task<ActionResult<CreateTrainerResultDto>> Create(CreateTrainerCommand command, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.TrainerId }, result);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCodes.Trainers.Manage)]
    public async Task<IActionResult> Update(Guid id, UpdateTrainerCommand command, CancellationToken cancellationToken)
    {
        await mediator.Send(command with { Id = id }, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/assignments")]
    [RequirePermission(PermissionCodes.Trainers.Manage)]
    public async Task<ActionResult<Guid>> AssignClient(Guid id, AssignClientCommand command, CancellationToken cancellationToken)
        => Ok(await mediator.Send(command with { TrainerId = id }, cancellationToken));

    [HttpPost("assignments/{assignmentId:guid}/end")]
    [RequirePermission(PermissionCodes.Trainers.Manage)]
    public async Task<IActionResult> EndAssignment(Guid assignmentId, CancellationToken cancellationToken)
    {
        await mediator.Send(new EndTrainerAssignmentCommand(assignmentId), cancellationToken);
        return NoContent();
    }

    [HttpPost("assignments/{assignmentId:guid}/sessions")]
    [RequirePermission(PermissionCodes.Trainers.Manage)]
    public async Task<ActionResult<Guid>> ScheduleSession(Guid assignmentId, ScheduleTrainerSessionCommand command, CancellationToken cancellationToken)
        => Ok(await mediator.Send(command with { TrainerAssignmentId = assignmentId }, cancellationToken));

    [HttpPost("sessions/{sessionId:guid}/complete")]
    [RequirePermission(PermissionCodes.Trainers.Manage)]
    public async Task<IActionResult> CompleteSession(Guid sessionId, CompleteTrainerSessionCommand command, CancellationToken cancellationToken)
    {
        await mediator.Send(command with { SessionId = sessionId }, cancellationToken);
        return NoContent();
    }

    [HttpPost("sessions/{sessionId:guid}/cancel")]
    [RequirePermission(PermissionCodes.Trainers.Manage)]
    public async Task<IActionResult> CancelSession(Guid sessionId, CancellationToken cancellationToken)
    {
        await mediator.Send(new CancelTrainerSessionCommand(sessionId), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/ratings")]
    [RequirePermission(PermissionCodes.Trainers.Manage)]
    public async Task<ActionResult<Guid>> AddRating(Guid id, AddTrainerRatingCommand command, CancellationToken cancellationToken)
        => Ok(await mediator.Send(command with { TrainerId = id }, cancellationToken));

    [HttpPost("{id:guid}/schedules")]
    [RequirePermission(PermissionCodes.Trainers.Manage)]
    public async Task<ActionResult<Guid>> AddSchedule(Guid id, CreateTrainerScheduleCommand command, CancellationToken cancellationToken)
        => Ok(await mediator.Send(command with { TrainerId = id }, cancellationToken));

    [HttpPost("{id:guid}/commissions")]
    [RequirePermission(PermissionCodes.Trainers.Manage)]
    public async Task<ActionResult<Guid>> AddCommission(Guid id, CreateCommissionRecordCommand command, CancellationToken cancellationToken)
        => Ok(await mediator.Send(command with { TrainerId = id }, cancellationToken));

    [HttpPut("commissions/{commissionRecordId:guid}/status")]
    [RequirePermission(PermissionCodes.Trainers.Manage)]
    public async Task<IActionResult> UpdateCommissionStatus(Guid commissionRecordId, UpdateCommissionStatusCommand command, CancellationToken cancellationToken)
    {
        await mediator.Send(command with { CommissionRecordId = commissionRecordId }, cancellationToken);
        return NoContent();
    }
}
