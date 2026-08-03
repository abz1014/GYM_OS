using GymOS.API.Authorization;
using GymOS.Application.Modules.Dashboard.Dtos;
using GymOS.Application.Modules.Dashboard.Queries;
using GymOS.Shared;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymOS.API.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public class DashboardController(ISender mediator) : ControllerBase
{
    [HttpGet("summary")]
    [RequirePermission(PermissionCodes.Dashboard.View)]
    public async Task<ActionResult<DashboardSummaryDto>> Summary([FromQuery] Guid? branchId, CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetDashboardSummaryQuery(branchId), cancellationToken));
}
