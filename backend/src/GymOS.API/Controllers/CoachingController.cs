using GymOS.API.Authorization;
using GymOS.Application.Modules.Coaching.Dtos;
using GymOS.Application.Modules.Coaching.Queries;
using GymOS.Shared;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymOS.API.Controllers;

/// <summary>
/// The trainer coaching dashboard's read-models — plateaus, workout/nutrition compliance, and risk
/// flags — across every active member in the caller's accessible branches. Gated on Trainers.View:
/// the design calls for either trainers.view or workouts.view, but RequirePermission only checks one
/// policy, and every seeded Trainer role already holds trainers.view alongside workouts.view.
/// </summary>
[ApiController]
[Authorize]
[Route("api/coaching")]
public class CoachingController(ISender mediator) : ControllerBase
{
    [HttpGet("plateaus")]
    [RequirePermission(PermissionCodes.Trainers.View)]
    public async Task<ActionResult<List<PlateauRowDto>>> Plateaus(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetCoachingPlateausQuery(), cancellationToken));

    [HttpGet("compliance")]
    [RequirePermission(PermissionCodes.Trainers.View)]
    public async Task<ActionResult<List<ComplianceRowDto>>> Compliance(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetCoachingComplianceQuery(), cancellationToken));

    [HttpGet("risks")]
    [RequirePermission(PermissionCodes.Trainers.View)]
    public async Task<ActionResult<List<RiskRowDto>>> Risks(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetCoachingRisksQuery(), cancellationToken));
}
