using GymOS.API.Authorization;
using GymOS.Application.Modules.Attendance.Dtos;
using GymOS.Application.Modules.Challenges.Commands;
using GymOS.Application.Modules.Challenges.Dtos;
using GymOS.Application.Modules.Challenges.Queries;
using GymOS.Application.Modules.Experience.Commands;
using GymOS.Application.Modules.Experience.Dtos;
using GymOS.Application.Modules.Experience.Queries;
using GymOS.Application.Modules.Members.Dtos;
using GymOS.Application.Modules.Nutrition.Dtos;
using GymOS.Application.Modules.Portal.Commands;
using GymOS.Application.Modules.Portal.Dtos;
using GymOS.Application.Modules.Portal.Queries;
using GymOS.Application.Modules.Workouts.Dtos;
using GymOS.Domain.Classes;
using GymOS.Domain.Experience;
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

    // Member self-logging. Before these existed the member could see every projection the Member
    // Experience Engine produces but had no way to produce one — only staff could log on their
    // behalf. Each delegates to the same command the staff surface uses, so the XP / personal-record
    // / mastery / streak / challenge cascade is identical no matter who logged it.
    [HttpGet("logging-options")]
    [RequirePermission(PermissionCodes.Portal.View)]
    public async Task<ActionResult<MyLoggingOptionsDto>> LoggingOptions(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetMyLoggingOptionsQuery(), cancellationToken));

    [HttpPost("workouts")]
    [RequirePermission(PermissionCodes.Portal.View)]
    public async Task<ActionResult<MyWorkoutResultDto>> LogWorkout(LogMyWorkoutCommand command, CancellationToken cancellationToken)
        => Ok(await mediator.Send(command, cancellationToken));

    [HttpPost("nutrition/meals")]
    [RequirePermission(PermissionCodes.Portal.View)]
    public async Task<ActionResult<Guid>> LogMeal(LogMyMealCommand command, CancellationToken cancellationToken)
        => Ok(await mediator.Send(command, cancellationToken));

    [HttpPost("nutrition/water")]
    [RequirePermission(PermissionCodes.Portal.View)]
    public async Task<ActionResult<Guid>> LogWater(LogMyWaterCommand command, CancellationToken cancellationToken)
        => Ok(await mediator.Send(command, cancellationToken));

    [HttpGet("measurements")]
    [RequirePermission(PermissionCodes.Portal.View)]
    public async Task<ActionResult<List<MyMeasurementDto>>> Measurements(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetMyMeasurementsQuery(), cancellationToken));

    [HttpPost("measurements")]
    [RequirePermission(PermissionCodes.Portal.View)]
    public async Task<ActionResult<Guid>> LogMeasurement(LogMyMeasurementCommand command, CancellationToken cancellationToken)
        => Ok(await mediator.Send(command, cancellationToken));

    /// <summary>The member home screen in one call — ring, streak, today's class and the top nudge.</summary>
    [HttpGet("today")]
    [RequirePermission(PermissionCodes.Portal.View)]
    public async Task<ActionResult<MyTodayDto>> Today(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetMyTodayQuery(), cancellationToken));

    [HttpPut("weekly-goal")]
    [RequirePermission(PermissionCodes.Portal.View)]
    public async Task<IActionResult> SetWeeklyGoal(SetMyWeeklyGoalCommand command, CancellationToken cancellationToken)
    {
        await mediator.Send(command, cancellationToken);
        return NoContent();
    }

    /// <summary>The member's branch leaderboard for a category and period.</summary>
    [HttpGet("leaderboard")]
    [RequirePermission(PermissionCodes.Portal.View)]
    public async Task<ActionResult<MyLeaderboardDto>> Leaderboard(
        [FromQuery] LeaderboardCategory category = LeaderboardCategory.XpEarned,
        [FromQuery] LeaderboardPeriod period = LeaderboardPeriod.Month,
        CancellationToken cancellationToken = default)
        => Ok(await mediator.Send(new GetMyLeaderboardQuery(category, period), cancellationToken));

    /// <summary>The pre-filled session a member can confirm in one tap — see GetMyNextSessionQuery.</summary>
    [HttpGet("next-session")]
    [RequirePermission(PermissionCodes.Portal.View)]
    public async Task<ActionResult<MySessionProposalDto>> NextSession(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetMyNextSessionQuery(), cancellationToken));

    /// <summary>Takes back a just-recorded session. Window enforced by WorkoutUndoPolicy.</summary>
    [HttpDelete("workouts/{workoutLogId:guid}")]
    [RequirePermission(PermissionCodes.Portal.View)]
    public async Task<IActionResult> UndoWorkout(Guid workoutLogId, CancellationToken cancellationToken)
    {
        await mediator.Send(new UndoMyWorkoutCommand(workoutLogId), cancellationToken);
        return NoContent();
    }

    [HttpGet("training-volume")]
    [RequirePermission(PermissionCodes.Portal.View)]
    public async Task<ActionResult<List<MyDailyVolumeDto>>> TrainingVolume(
        [FromQuery] int daysBack = 30, CancellationToken cancellationToken = default)
        => Ok(await mediator.Send(new GetMyTrainingVolumeQuery(daysBack), cancellationToken));

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

    [HttpGet("referrals")]
    [RequirePermission(PermissionCodes.Portal.View)]
    public async Task<ActionResult<MyReferralsDto>> Referrals(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetMyReferralsQuery(), cancellationToken));

    // Workout/nutrition intelligence: derived from the member's own logged history, not staff input.
    [HttpGet("nutrition/summary")]
    [RequirePermission(PermissionCodes.Portal.View)]
    public async Task<ActionResult<MyNutritionSummaryDto>> NutritionSummary(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetMyNutritionSummaryQuery(), cancellationToken));

    [HttpGet("workout-suggestions")]
    [RequirePermission(PermissionCodes.Portal.View)]
    public async Task<ActionResult<List<MyExerciseSuggestionDto>>> WorkoutSuggestions(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetMyWorkoutSuggestionsQuery(), cancellationToken));

    // Member Experience Engine: the member's level, XP, and recent XP ledger. Self-scoped like the
    // rest of /api/me — no member id is accepted from the caller.
    [HttpGet("experience")]
    [RequirePermission(PermissionCodes.Portal.View)]
    public async Task<ActionResult<MyExperienceDto>> Experience(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetMyExperienceQuery(), cancellationToken));

    [HttpGet("personal-records")]
    [RequirePermission(PermissionCodes.Portal.View)]
    public async Task<ActionResult<List<MyPersonalRecordDto>>> PersonalRecords(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetMyPersonalRecordsQuery(), cancellationToken));

    [HttpGet("mastery")]
    [RequirePermission(PermissionCodes.Portal.View)]
    public async Task<ActionResult<MyMasteryDto>> Mastery(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetMyMasteryQuery(), cancellationToken));

    [HttpGet("achievements")]
    [RequirePermission(PermissionCodes.Portal.View)]
    public async Task<ActionResult<List<MyAchievementDto>>> Achievements(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetMyAchievementsQuery(), cancellationToken));

    [HttpGet("streaks")]
    [RequirePermission(PermissionCodes.Portal.View)]
    public async Task<ActionResult<MyStreaksDto>> Streaks(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetMyStreaksQuery(), cancellationToken));

    // Recovery: the member's training-load recovery status (overall + per muscle group) and logging a
    // rest / recovery day, which earns recovery XP once per day. Self-scoped like the rest of /api/me.
    [HttpGet("recovery")]
    [RequirePermission(PermissionCodes.Portal.View)]
    public async Task<ActionResult<MyRecoveryDto>> Recovery(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetMyRecoveryQuery(), cancellationToken));

    [HttpPost("recovery/log")]
    [RequirePermission(PermissionCodes.Portal.View)]
    public async Task<ActionResult<Guid>> LogRecovery(LogMyRecoveryCommand command, CancellationToken cancellationToken)
        => Ok(await mediator.Send(command, cancellationToken));

    // Recommendations: plateau/focus/volume/recovery/skill-tree nudges, each always explained.
    [HttpGet("recommendations")]
    [RequirePermission(PermissionCodes.Portal.View)]
    public async Task<ActionResult<List<MyRecommendationDto>>> Recommendations(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetMyRecommendationsQuery(), cancellationToken));

    // Transformation timeline: measurements, photos, achieved goals, PRs, and achievements merged
    // into one chronological feed.
    [HttpGet("timeline")]
    [RequirePermission(PermissionCodes.Portal.View)]
    public async Task<ActionResult<List<MyTimelineEntryDto>>> Timeline(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetMyTimelineQuery(), cancellationToken));

    // Community challenges: browse what's available (tenant-wide + own branch), join, and leave.
    // Completion + challenge XP happen automatically off logged workouts, not through an action here.
    [HttpGet("challenges")]
    [RequirePermission(PermissionCodes.Portal.View)]
    public async Task<ActionResult<List<MyChallengeDto>>> Challenges(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetMyChallengesQuery(), cancellationToken));

    [HttpPost("challenges/{challengeId:guid}/join")]
    [RequirePermission(PermissionCodes.Portal.View)]
    public async Task<IActionResult> JoinChallenge(Guid challengeId, CancellationToken cancellationToken)
    {
        await mediator.Send(new JoinChallengeCommand(challengeId), cancellationToken);
        return NoContent();
    }

    [HttpPost("challenges/{challengeId:guid}/leave")]
    [RequirePermission(PermissionCodes.Portal.View)]
    public async Task<IActionResult> LeaveChallenge(Guid challengeId, CancellationToken cancellationToken)
    {
        await mediator.Send(new LeaveChallengeCommand(challengeId), cancellationToken);
        return NoContent();
    }
}
