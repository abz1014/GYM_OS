using GymOS.API.Authorization;
using GymOS.Application.Modules.Workouts.Commands;
using GymOS.Application.Modules.Workouts.Dtos;
using GymOS.Application.Modules.Workouts.Queries;
using GymOS.Shared;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymOS.API.Controllers;

[ApiController]
[Authorize]
[Route("api/workouts")]
public class WorkoutsController(ISender mediator) : ControllerBase
{
    [HttpGet("exercises")]
    [RequirePermission(PermissionCodes.Workouts.View)]
    public async Task<ActionResult<List<ExerciseDto>>> Exercises(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetExercisesListQuery(), cancellationToken));

    [HttpPost("exercises")]
    [RequirePermission(PermissionCodes.Workouts.Manage)]
    public async Task<ActionResult<Guid>> CreateExercise(CreateExerciseCommand command, CancellationToken cancellationToken)
        => Ok(await mediator.Send(command, cancellationToken));

    [HttpGet("templates")]
    [RequirePermission(PermissionCodes.Workouts.View)]
    public async Task<ActionResult<List<WorkoutTemplateListItemDto>>> Templates(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetWorkoutTemplatesListQuery(), cancellationToken));

    [HttpGet("templates/{id:guid}")]
    [RequirePermission(PermissionCodes.Workouts.View)]
    public async Task<ActionResult<WorkoutTemplateDetailDto>> GetTemplateById(Guid id, CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetWorkoutTemplateByIdQuery(id), cancellationToken));

    [HttpPost("templates")]
    [RequirePermission(PermissionCodes.Workouts.Manage)]
    public async Task<ActionResult<Guid>> CreateTemplate(CreateWorkoutTemplateCommand command, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetTemplateById), new { id }, id);
    }

    [HttpGet("logs/member/{memberId:guid}")]
    [RequirePermission(PermissionCodes.Workouts.View)]
    public async Task<ActionResult<List<WorkoutLogDto>>> MemberLogs(Guid memberId, CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetMemberWorkoutLogsQuery(memberId), cancellationToken));

    [HttpPost("logs")]
    [RequirePermission(PermissionCodes.Workouts.Manage)]
    public async Task<ActionResult<Guid>> LogWorkout(LogWorkoutCommand command, CancellationToken cancellationToken)
        => Ok(await mediator.Send(command, cancellationToken));

    [HttpGet("assignments/member/{memberId:guid}")]
    [RequirePermission(PermissionCodes.Workouts.View)]
    public async Task<ActionResult<List<WorkoutAssignmentListItemDto>>> MemberAssignments(Guid memberId, CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetMemberWorkoutAssignmentsQuery(memberId), cancellationToken));

    [HttpPost("assignments")]
    [RequirePermission(PermissionCodes.Workouts.Manage)]
    public async Task<ActionResult<Guid>> AssignTemplate(AssignWorkoutTemplateCommand command, CancellationToken cancellationToken)
        => Ok(await mediator.Send(command, cancellationToken));
}
