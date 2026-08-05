using GymOS.API.Authorization;
using GymOS.Application.Modules.Challenges.Commands;
using GymOS.Application.Modules.Challenges.Dtos;
using GymOS.Application.Modules.Challenges.Queries;
using GymOS.Shared;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymOS.API.Controllers;

/// <summary>Staff management of community challenges — list + create. Members never see this
/// controller; their join/leave/view surface is entirely under PortalController's /api/me/challenges.</summary>
[ApiController]
[Authorize]
[Route("api/challenges")]
public class ChallengesController(ISender mediator) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.Experience.Manage)]
    public async Task<ActionResult<List<ChallengeListItemDto>>> List(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetChallengesListQuery(), cancellationToken));

    [HttpPost]
    [RequirePermission(PermissionCodes.Experience.Manage)]
    public async Task<ActionResult<Guid>> Create(CreateChallengeCommand command, CancellationToken cancellationToken)
        => Ok(await mediator.Send(command, cancellationToken));
}
