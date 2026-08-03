using GymOS.Application.Modules.Settings.Dtos;
using GymOS.Application.Modules.Settings.Queries;
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
    public async Task<ActionResult<List<BranchDto>>> List(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetBranchesQuery(), cancellationToken));
}
