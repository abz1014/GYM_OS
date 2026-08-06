using GymOS.API.Authorization;
using GymOS.Application.Modules.Coaching.Commands;
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
    // The trainer's half of the member conversation. A memberId is accepted here — unlike anywhere
    // on /api/me — and is safe only because every handler checks the pairing between the ACTING
    // trainer (resolved from the JWT) and that member before reading or writing a word.
    [HttpGet("clients/{memberId:guid}/messages")]
    [RequirePermission(PermissionCodes.Trainers.View)]
    public async Task<ActionResult<CoachConversationDto>> ClientMessages(Guid memberId, CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetMyClientConversationQuery(memberId), cancellationToken));

    [HttpPost("clients/{memberId:guid}/messages")]
    [RequirePermission(PermissionCodes.Trainers.View)]
    public async Task<ActionResult<Guid>> MessageClient(Guid memberId, MessageMyClientBody body, CancellationToken cancellationToken)
        => Ok(await mediator.Send(new MessageMyClientCommand(memberId, body.Body, body.WorkoutLogId), cancellationToken));

    [HttpPost("clients/{memberId:guid}/messages/read")]
    [RequirePermission(PermissionCodes.Trainers.View)]
    public async Task<IActionResult> ReadClientMessages(Guid memberId, CancellationToken cancellationToken)
    {
        await mediator.Send(new ReadMyClientMessagesCommand(memberId), cancellationToken);
        return NoContent();
    }

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

/// <summary>Body of a trainer-to-client message; the member comes from the route.</summary>
public record MessageMyClientBody(string Body, Guid? WorkoutLogId);
