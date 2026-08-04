using GymOS.API.Authorization;
using GymOS.Application.Modules.Inventory.Commands;
using GymOS.Application.Modules.Inventory.Dtos;
using GymOS.Application.Modules.Inventory.Queries;
using GymOS.Domain.Inventory;
using GymOS.Shared;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymOS.API.Controllers;

[ApiController]
[Authorize]
[Route("api/inventory")]
public class InventoryController(ISender mediator) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.Inventory.View)]
    public async Task<ActionResult<PagedList<InventoryItemListDto>>> List(
        [FromQuery] Guid? branchId, [FromQuery] InventoryCategory? category, [FromQuery] bool? lowStockOnly,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        => Ok(await mediator.Send(new GetInventoryItemsListQuery(branchId, category, lowStockOnly, page, pageSize), cancellationToken));

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCodes.Inventory.View)]
    public async Task<ActionResult<InventoryItemDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetInventoryItemByIdQuery(id), cancellationToken));

    [HttpPost]
    [RequirePermission(PermissionCodes.Inventory.Manage)]
    public async Task<ActionResult<Guid>> Create(CreateInventoryItemCommand command, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPost("{id:guid}/movements")]
    [RequirePermission(PermissionCodes.Inventory.Manage)]
    public async Task<ActionResult<Guid>> RecordMovement(Guid id, RecordStockMovementCommand command, CancellationToken cancellationToken)
        => Ok(await mediator.Send(command with { InventoryItemId = id }, cancellationToken));

    [HttpPost("{id:guid}/purchases")]
    [RequirePermission(PermissionCodes.Inventory.Manage)]
    public async Task<ActionResult<Guid>> RecordPurchase(Guid id, RecordPurchaseCommand command, CancellationToken cancellationToken)
        => Ok(await mediator.Send(command with { InventoryItemId = id }, cancellationToken));
}
