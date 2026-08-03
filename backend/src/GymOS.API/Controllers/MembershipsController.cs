using GymOS.API.Authorization;
using GymOS.Application.Modules.Memberships.Commands;
using GymOS.Application.Modules.Memberships.Dtos;
using GymOS.Application.Modules.Memberships.Queries;
using GymOS.Shared;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymOS.API.Controllers;

[ApiController]
[Authorize]
[Route("api/membership-plans")]
public class MembershipsController(ISender mediator) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.Memberships.View)]
    public async Task<ActionResult<List<MembershipPlanDto>>> List([FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default)
        => Ok(await mediator.Send(new GetMembershipPlansQuery(includeInactive), cancellationToken));

    [HttpPost]
    [RequirePermission(PermissionCodes.Memberships.ManagePlans)]
    public async Task<ActionResult<Guid>> Create(CreateMembershipPlanCommand command, CancellationToken cancellationToken)
        => Ok(await mediator.Send(command, cancellationToken));

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCodes.Memberships.ManagePlans)]
    public async Task<IActionResult> Update(Guid id, UpdateMembershipPlanCommand command, CancellationToken cancellationToken)
    {
        if (id != command.Id)
        {
            return BadRequest("Route id and body id must match.");
        }

        await mediator.Send(command, cancellationToken);
        return NoContent();
    }

    [HttpPost("discounts")]
    [RequirePermission(PermissionCodes.Memberships.ManageDiscounts)]
    public async Task<ActionResult<Guid>> CreateDiscount(CreateDiscountCommand command, CancellationToken cancellationToken)
        => Ok(await mediator.Send(command, cancellationToken));

    [HttpPost("coupons")]
    [RequirePermission(PermissionCodes.Memberships.ManageDiscounts)]
    public async Task<ActionResult<Guid>> CreateCoupon(CreateCouponCommand command, CancellationToken cancellationToken)
        => Ok(await mediator.Send(command, cancellationToken));
}
