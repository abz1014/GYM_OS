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

    [HttpPost("{id:guid}/assignments")]
    [RequirePermission(PermissionCodes.Trainers.Manage)]
    public async Task<ActionResult<Guid>> AssignClient(Guid id, AssignClientCommand command, CancellationToken cancellationToken)
        => Ok(await mediator.Send(command with { TrainerId = id }, cancellationToken));

    [HttpPost("{id:guid}/ratings")]
    [RequirePermission(PermissionCodes.Trainers.Manage)]
    public async Task<ActionResult<Guid>> AddRating(Guid id, AddTrainerRatingCommand command, CancellationToken cancellationToken)
        => Ok(await mediator.Send(command with { TrainerId = id }, cancellationToken));
}
