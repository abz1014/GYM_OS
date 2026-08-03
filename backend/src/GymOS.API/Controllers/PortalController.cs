using GymOS.API.Authorization;
using GymOS.Application.Modules.Attendance.Dtos;
using GymOS.Application.Modules.Members.Dtos;
using GymOS.Application.Modules.Nutrition.Dtos;
using GymOS.Application.Modules.Portal.Queries;
using GymOS.Application.Modules.Workouts.Dtos;
using GymOS.Shared;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymOS.API.Controllers;

/// <summary>
/// The member self-service surface. Every action here takes NO memberId/entity-id parameter —
/// "whose data" is resolved server-side from the JWT (see MyMemberResolver), not accepted from
/// the caller. That is the whole point: the staff-facing equivalents (AttendanceController,
/// WorkoutsController, NutritionController) trust a caller-supplied member id because they're
/// gated by staff-wide view permissions; handing that same shape to the Member role let one
/// member read another member's attendance, workouts, and nutrition data.
/// </summary>
[ApiController]
[Authorize]
[Route("api/me")]
public class PortalController(ISender mediator) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.Portal.View)]
    public async Task<ActionResult<MemberDetailDto>> Profile(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetMyProfileQuery(), cancellationToken));

    [HttpGet("attendance")]
    [RequirePermission(PermissionCodes.Portal.View)]
    public async Task<ActionResult<PagedList<AttendanceRecordDto>>> Attendance(
        [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        => Ok(await mediator.Send(new GetMyAttendanceQuery(fromDate, toDate, page, pageSize), cancellationToken));

    [HttpGet("workouts")]
    [RequirePermission(PermissionCodes.Portal.View)]
    public async Task<ActionResult<List<WorkoutLogDto>>> Workouts(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetMyWorkoutLogsQuery(), cancellationToken));

    [HttpGet("nutrition/diet-plans")]
    [RequirePermission(PermissionCodes.Portal.View)]
    public async Task<ActionResult<List<DietPlanListItemDto>>> DietPlans(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetMyDietPlansQuery(), cancellationToken));

    [HttpGet("nutrition/water")]
    [RequirePermission(PermissionCodes.Portal.View)]
    public async Task<ActionResult<List<WaterLogDto>>> WaterLogs(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetMyWaterLogsQuery(), cancellationToken));
}
