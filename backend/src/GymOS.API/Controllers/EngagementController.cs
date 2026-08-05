using GymOS.API.Authorization;
using GymOS.Application.Modules.Engagement.Dtos;
using GymOS.Application.Modules.Engagement.Queries;
using GymOS.Shared;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymOS.API.Controllers;

/// <summary>
/// The manager engagement dashboard — extends the existing Reports/Analytics surface rather than
/// replacing it, so it's gated the same way every other report is: Reports.View.
/// </summary>
[ApiController]
[Authorize]
[Route("api/engagement")]
public class EngagementController(ISender mediator) : ControllerBase
{
    [HttpGet("summary")]
    [RequirePermission(PermissionCodes.Reports.View)]
    public async Task<ActionResult<EngagementSummaryDto>> Summary(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetEngagementSummaryQuery(), cancellationToken));
}
