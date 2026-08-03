using GymOS.API.Authorization;
using GymOS.Application.Modules.Settings.Commands;
using GymOS.Application.Modules.Settings.Dtos;
using GymOS.Application.Modules.Settings.Queries;
using GymOS.Shared;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymOS.API.Controllers;

[ApiController]
[Authorize]
[Route("api/branches")]
public class BranchesController(ISender mediator) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.Dashboard.View)]
    public async Task<ActionResult<List<BranchDto>>> List([FromQuery] bool includeInactive, CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetBranchesQuery(includeInactive), cancellationToken));

    [HttpPost]
    [RequirePermission(PermissionCodes.Settings.ManageBranches)]
    public async Task<ActionResult<Guid>> Create(CreateBranchCommand command, CancellationToken cancellationToken)
        => Ok(await mediator.Send(command, cancellationToken));

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCodes.Settings.ManageBranches)]
    public async Task<IActionResult> Update(Guid id, UpdateBranchCommand command, CancellationToken cancellationToken)
    {
        if (id != command.Id)
        {
            return BadRequest("Route id and body id must match.");
        }

        await mediator.Send(command, cancellationToken);
        return NoContent();
    }
}
