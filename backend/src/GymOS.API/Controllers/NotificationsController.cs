using GymOS.API.Authorization;
using GymOS.Application.Modules.Notifications.Commands;
using GymOS.Application.Modules.Notifications.Dtos;
using GymOS.Application.Modules.Notifications.Queries;
using GymOS.Domain.Notifications;
using GymOS.Shared;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymOS.API.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
public class NotificationsController(ISender mediator) : ControllerBase
{
    [HttpGet("templates")]
    [RequirePermission(PermissionCodes.Notifications.View)]
    public async Task<ActionResult<List<NotificationTemplateDto>>> Templates(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetNotificationTemplatesQuery(), cancellationToken));

    [HttpPut("templates/{id:guid}")]
    [RequirePermission(PermissionCodes.Notifications.Manage)]
    public async Task<IActionResult> UpdateTemplate(Guid id, UpdateNotificationTemplateCommand command, CancellationToken cancellationToken)
    {
        await mediator.Send(command with { Id = id }, cancellationToken);
        return NoContent();
    }

    [HttpGet("scheduled")]
    [RequirePermission(PermissionCodes.Notifications.View)]
    public async Task<ActionResult<List<ScheduledNotificationDto>>> Scheduled(ScheduledNotificationStatus? status, CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetScheduledNotificationsQuery(status), cancellationToken));

    [HttpGet("logs")]
    [RequirePermission(PermissionCodes.Notifications.View)]
    public async Task<ActionResult<List<NotificationLogDto>>> Logs(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetNotificationLogsQuery(), cancellationToken));

    [HttpPost("run-checks")]
    [RequirePermission(PermissionCodes.Notifications.Manage)]
    public async Task<ActionResult<TriggerNotificationChecksResultDto>> RunChecks(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new TriggerNotificationChecksCommand(), cancellationToken));
}
