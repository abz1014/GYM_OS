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
[Route("api/settings")]
public class SettingsController(ISender mediator) : ControllerBase
{
    [HttpGet("gym-profile")]
    [RequirePermission(PermissionCodes.Settings.View)]
    public async Task<ActionResult<GymProfileDto>> GetGymProfile(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetGymProfileQuery(), cancellationToken));

    [HttpPut("gym-profile")]
    [RequirePermission(PermissionCodes.Settings.ManageGymProfile)]
    public async Task<IActionResult> UpdateGymProfile(UpdateGymProfileCommand command, CancellationToken cancellationToken)
    {
        await mediator.Send(command, cancellationToken);
        return NoContent();
    }
}
