using GymOS.API.Authorization;
using GymOS.Application.Modules.Attendance.Dtos;
using GymOS.Application.Modules.Members.Dtos;
using GymOS.Application.Modules.Nutrition.Dtos;
using GymOS.Application.Modules.Portal.Commands;
using GymOS.Application.Modules.Portal.Dtos;
using GymOS.Application.Modules.Portal.Queries;
using GymOS.Application.Modules.Workouts.Dtos;
using GymOS.Domain.Classes;
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

    [HttpGet("workout-assignments")]
    [RequirePermission(PermissionCodes.Portal.View)]
    public async Task<ActionResult<List<WorkoutAssignmentListItemDto>>> WorkoutAssignments(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetMyWorkoutAssignmentsQuery(), cancellationToken));

    [HttpGet("nutrition/diet-plans")]
    [RequirePermission(PermissionCodes.Portal.View)]
    public async Task<ActionResult<List<DietPlanListItemDto>>> DietPlans(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetMyDietPlansQuery(), cancellationToken));

    [HttpGet("nutrition/water")]
    [RequirePermission(PermissionCodes.Portal.View)]
    public async Task<ActionResult<List<WaterLogDto>>> WaterLogs(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetMyWaterLogsQuery(), cancellationToken));

    // Member self-service class booking. Gated by Portal.View (the member-portal access permission);
    // the real safety is that identity comes from the JWT via MyMemberResolver, so a member can only
    // ever see and book their own branch's classes and cancel their own bookings.
    [HttpGet("classes")]
    [RequirePermission(PermissionCodes.Portal.View)]
    public async Task<ActionResult<List<MyClassSessionDto>>> Classes(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetMyClassScheduleQuery(), cancellationToken));

    [HttpGet("class-bookings")]
    [RequirePermission(PermissionCodes.Portal.View)]
    public async Task<ActionResult<List<MyClassBookingDto>>> ClassBookings(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetMyClassBookingsQuery(), cancellationToken));

    [HttpPost("classes/{sessionId:guid}/book")]
    [RequirePermission(PermissionCodes.Portal.View)]
    public async Task<ActionResult<ClassBookingStatus>> BookClass(Guid sessionId, CancellationToken cancellationToken)
        => Ok(await mediator.Send(new BookMyClassCommand(sessionId), cancellationToken));

    [HttpPost("class-bookings/{bookingId:guid}/cancel")]
    [RequirePermission(PermissionCodes.Portal.View)]
    public async Task<IActionResult> CancelClassBooking(Guid bookingId, CancellationToken cancellationToken)
    {
        await mediator.Send(new CancelMyClassBookingCommand(bookingId), cancellationToken);
        return NoContent();
    }

    // Progress & goals: the member's own streak/visits/weight-trend snapshot plus self-set goals.
    // Same identity rule as everything above — no member id is ever accepted from the caller.
    [HttpGet("progress")]
    [RequirePermission(PermissionCodes.Portal.View)]
    public async Task<ActionResult<MyProgressDto>> Progress(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetMyProgressQuery(), cancellationToken));

    [HttpPost("goals")]
    [RequirePermission(PermissionCodes.Portal.View)]
    public async Task<ActionResult<Guid>> CreateGoal(CreateMyGoalCommand command, CancellationToken cancellationToken)
        => Ok(await mediator.Send(command, cancellationToken));

    [HttpPost("goals/{goalId:guid}/achieve")]
    [RequirePermission(PermissionCodes.Portal.View)]
    public async Task<IActionResult> AchieveGoal(Guid goalId, CancellationToken cancellationToken)
    {
        await mediator.Send(new AchieveMyGoalCommand(goalId), cancellationToken);
        return NoContent();
    }
}
