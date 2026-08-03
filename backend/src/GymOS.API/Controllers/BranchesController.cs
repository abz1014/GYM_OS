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
    public async Task<ActionResult<List<BranchDto>>> List(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetBranchesQuery(), cancellationToken));

    [HttpPost]
    [RequirePermission(PermissionCodes.Settings.ManageBranches)]
    public async Task<ActionResult<Guid>> Create(CreateBranchCommand command, CancellationToken cancellationToken)
        => Ok(await mediator.Send(command, cancellationToken));
}
