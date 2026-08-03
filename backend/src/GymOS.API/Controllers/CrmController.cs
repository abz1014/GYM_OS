using GymOS.API.Authorization;
using GymOS.Application.Modules.Crm.Commands;
using GymOS.Application.Modules.Crm.Dtos;
using GymOS.Application.Modules.Crm.Queries;
using GymOS.Domain.Crm;
using GymOS.Shared;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymOS.API.Controllers;

[ApiController]
[Authorize]
[Route("api/leads")]
public class CrmController(ISender mediator) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.Crm.View)]
    public async Task<ActionResult<PagedList<LeadListItemDto>>> List(
        [FromQuery] LeadStage? stage, [FromQuery] Guid? branchId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        => Ok(await mediator.Send(new GetLeadsListQuery(stage, branchId, page, pageSize), cancellationToken));

    [HttpGet("summary")]
    [RequirePermission(PermissionCodes.Crm.View)]
    public async Task<ActionResult<CrmPipelineSummaryDto>> Summary([FromQuery] Guid? branchId, CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetCrmPipelineSummaryQuery(branchId), cancellationToken));

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCodes.Crm.View)]
    public async Task<ActionResult<LeadDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetLeadByIdQuery(id), cancellationToken));

    [HttpPost]
    [RequirePermission(PermissionCodes.Crm.ManageLeads)]
    public async Task<ActionResult<Guid>> Create(CreateLeadCommand command, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPut("{id:guid}/stage")]
    [RequirePermission(PermissionCodes.Crm.ManageLeads)]
    public async Task<IActionResult> UpdateStage(Guid id, UpdateLeadStageCommand command, CancellationToken cancellationToken)
    {
        await mediator.Send(command with { LeadId = id }, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/activities")]
    [RequirePermission(PermissionCodes.Crm.ManageLeads)]
    public async Task<ActionResult<Guid>> AddActivity(Guid id, AddLeadActivityCommand command, CancellationToken cancellationToken)
        => Ok(await mediator.Send(command with { LeadId = id }, cancellationToken));

    [HttpPost("activities/{activityId:guid}/complete")]
    [RequirePermission(PermissionCodes.Crm.ManageLeads)]
    public async Task<IActionResult> CompleteActivity(Guid activityId, CancellationToken cancellationToken)
    {
        await mediator.Send(new CompleteLeadActivityCommand(activityId), cancellationToken);
        return NoContent();
    }
}
