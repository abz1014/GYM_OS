using GymOS.API.Authorization;
using GymOS.Application.Modules.Settings.Commands;
using GymOS.Application.Modules.Settings.Dtos;
using GymOS.Application.Modules.Settings.Queries;
using GymOS.Shared;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymOS.API.Controllers;

[ApiController]
[Authorize]
[Route("api/settings")]
public class SettingsController(ISender mediator) : ControllerBase
{
    [HttpGet("gym-profile")]
    [RequirePermission(PermissionCodes.Settings.View)]
    public async Task<ActionResult<GymProfileDto>> GetGymProfile(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetGymProfileQuery(), cancellationToken));

    [HttpPut("gym-profile")]
    [RequirePermission(PermissionCodes.Settings.ManageGymProfile)]
    public async Task<IActionResult> UpdateGymProfile(UpdateGymProfileCommand command, CancellationToken cancellationToken)
    {
        await mediator.Send(command, cancellationToken);
        return NoContent();
    }

    [HttpGet("permission-matrix")]
    [RequirePermission(PermissionCodes.Settings.ManagePermissions)]
    public async Task<ActionResult<PermissionMatrixDto>> PermissionMatrix(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetPermissionMatrixQuery(), cancellationToken));

    [HttpPut("permission-matrix")]
    [RequirePermission(PermissionCodes.Settings.ManagePermissions)]
    public async Task<IActionResult> SetRolePermission(SetRolePermissionCommand command, CancellationToken cancellationToken)
    {
        await mediator.Send(command, cancellationToken);
        return NoContent();
    }

    [HttpGet("system-preferences")]
    [RequirePermission(PermissionCodes.Settings.View)]
    public async Task<ActionResult<List<SystemPreferenceDto>>> SystemPreferences(
        [FromQuery] Guid? branchId, CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetSystemPreferencesQuery(branchId), cancellationToken));

    // No dedicated "manage preferences" permission code exists in the catalog — reusing
    // ManageGymProfile since both are general gym-configuration concerns owned by Owner/Manager.
    [HttpPut("system-preferences")]
    [RequirePermission(PermissionCodes.Settings.ManageGymProfile)]
    public async Task<ActionResult<Guid>> UpsertSystemPreference(UpsertSystemPreferenceCommand command, CancellationToken cancellationToken)
        => Ok(await mediator.Send(command, cancellationToken));

    [HttpGet("audit-log")]
    [RequirePermission(PermissionCodes.Settings.View)]
    public async Task<ActionResult<PagedList<AuditLogDto>>> AuditLog(
        [FromQuery] string? entityType, [FromQuery] Guid? userId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
        => Ok(await mediator.Send(new GetAuditLogsQuery(entityType, userId, page, pageSize), cancellationToken));
}
