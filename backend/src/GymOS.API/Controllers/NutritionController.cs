using GymOS.API.Authorization;
using GymOS.Application.Modules.Nutrition.Commands;
using GymOS.Application.Modules.Nutrition.Dtos;
using GymOS.Application.Modules.Nutrition.Queries;
using GymOS.Shared;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymOS.API.Controllers;

[ApiController]
[Authorize]
[Route("api/nutrition")]
public class NutritionController(ISender mediator) : ControllerBase
{
    [HttpGet("food-items")]
    [RequirePermission(PermissionCodes.Nutrition.View)]
    public async Task<ActionResult<List<FoodItemDto>>> FoodItems(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetFoodItemsListQuery(), cancellationToken));

    [HttpPost("food-items")]
    [RequirePermission(PermissionCodes.Nutrition.Manage)]
    public async Task<ActionResult<Guid>> CreateFoodItem(CreateFoodItemCommand command, CancellationToken cancellationToken)
        => Ok(await mediator.Send(command, cancellationToken));

    /// <summary>
    /// The caller's own clients. Nutrition.View rather than Manage: a manager checking whether the
    /// nutritionist has a caseload holds View, and this returns THEIR clients — an empty roster for
    /// someone who has never written a plan, which is the honest answer rather than a hidden screen.
    /// </summary>
    [HttpGet("my-clients")]
    [RequirePermission(PermissionCodes.Nutrition.View)]
    public async Task<ActionResult<MyNutritionClientsDto>> MyClients(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetMyNutritionClientsQuery(), cancellationToken));

    [HttpGet("diet-plans/member/{memberId:guid}")]
    [RequirePermission(PermissionCodes.Nutrition.View)]
    public async Task<ActionResult<List<DietPlanListItemDto>>> MemberDietPlans(Guid memberId, CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetMemberDietPlansQuery(memberId), cancellationToken));

    [HttpGet("diet-plans/{id:guid}")]
    [RequirePermission(PermissionCodes.Nutrition.View)]
    public async Task<ActionResult<DietPlanDetailDto>> GetDietPlanById(Guid id, CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetDietPlanByIdQuery(id), cancellationToken));

    [HttpPost("diet-plans")]
    [RequirePermission(PermissionCodes.Nutrition.Manage)]
    public async Task<ActionResult<Guid>> CreateDietPlan(CreateDietPlanCommand command, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetDietPlanById), new { id }, id);
    }

    [HttpPost("diet-plans/{id:guid}/meals")]
    [RequirePermission(PermissionCodes.Nutrition.Manage)]
    public async Task<ActionResult<Guid>> AddMealEntry(Guid id, AddMealEntryCommand command, CancellationToken cancellationToken)
        => Ok(await mediator.Send(command with { DietPlanId = id }, cancellationToken));

    [HttpGet("water/member/{memberId:guid}")]
    [RequirePermission(PermissionCodes.Nutrition.View)]
    public async Task<ActionResult<List<WaterLogDto>>> MemberWaterLogs(Guid memberId, CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetMemberWaterLogsQuery(memberId), cancellationToken));

    [HttpPost("water")]
    [RequirePermission(PermissionCodes.Nutrition.Manage)]
    public async Task<ActionResult<Guid>> LogWater(LogWaterCommand command, CancellationToken cancellationToken)
        => Ok(await mediator.Send(command, cancellationToken));
}
