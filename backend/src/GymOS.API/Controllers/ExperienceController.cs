using GymOS.API.Authorization;
using GymOS.Application.Modules.Experience.Commands;
using GymOS.Shared;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymOS.API.Controllers;

/// <summary>
/// Staff-side maintenance surface for the Member Experience Engine, distinct from the member-facing
/// /api/me/* portal and from ChallengesController's content management. Currently just the projection
/// rebuild; future MEE admin actions belong here too.
/// </summary>
[ApiController]
[Authorize]
[Route("api/experience")]
public class ExperienceController(ISender mediator) : ControllerBase
{
    [HttpPost("rebuild-projections")]
    [RequirePermission(PermissionCodes.Experience.Manage)]
    public async Task<ActionResult<RebuildExperienceProjectionsResult>> RebuildProjections(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new RebuildExperienceProjectionsCommand(), cancellationToken));
}
