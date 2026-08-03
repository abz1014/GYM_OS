using GymOS.API.Authorization;
using GymOS.Application.Modules.Equipment.Commands;
using GymOS.Application.Modules.Equipment.Dtos;
using GymOS.Application.Modules.Equipment.Queries;
using GymOS.Domain.Equipment;
using GymOS.Shared;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymOS.API.Controllers;

[ApiController]
[Authorize]
[Route("api/equipment")]
public class EquipmentController(ISender mediator) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.Equipment.View)]
    public async Task<ActionResult<PagedList<AssetListItemDto>>> List(
        [FromQuery] Guid? branchId, [FromQuery] AssetStatus? status, [FromQuery] string? category,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        => Ok(await mediator.Send(new GetAssetsListQuery(branchId, status, category, page, pageSize), cancellationToken));

    [HttpGet("suppliers")]
    [RequirePermission(PermissionCodes.Equipment.View)]
    public async Task<ActionResult<List<SupplierDto>>> Suppliers(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetSuppliersListQuery(), cancellationToken));

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCodes.Equipment.View)]
    public async Task<ActionResult<AssetDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetAssetByIdQuery(id), cancellationToken));

    [HttpPost]
    [RequirePermission(PermissionCodes.Equipment.Manage)]
    public async Task<ActionResult<Guid>> Create(CreateAssetCommand command, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPost("suppliers")]
    [RequirePermission(PermissionCodes.Equipment.Manage)]
    public async Task<ActionResult<Guid>> CreateSupplier(CreateSupplierCommand command, CancellationToken cancellationToken)
        => Ok(await mediator.Send(command, cancellationToken));

    [HttpPut("{id:guid}/status")]
    [RequirePermission(PermissionCodes.Equipment.Manage)]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateAssetStatusCommand command, CancellationToken cancellationToken)
    {
        await mediator.Send(command with { AssetId = id }, cancellationToken);
        return NoContent();
    }
}
