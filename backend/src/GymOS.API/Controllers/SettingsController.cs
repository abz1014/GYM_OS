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

    [HttpGet("staff")]
    [RequirePermission(PermissionCodes.Settings.ManageStaff)]
    public async Task<ActionResult<StaffListDto>> Staff(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetStaffListQuery(), cancellationToken));

    [HttpPost("staff")]
    [RequirePermission(PermissionCodes.Settings.ManageStaff)]
    public async Task<ActionResult<CreateStaffResultDto>> CreateStaff(CreateStaffCommand command, CancellationToken cancellationToken)
        => Ok(await mediator.Send(command, cancellationToken));

    [HttpPut("staff/{id:guid}")]
    [RequirePermission(PermissionCodes.Settings.ManageStaff)]
    public async Task<IActionResult> UpdateStaff(Guid id, UpdateStaffCommand command, CancellationToken cancellationToken)
    {
        await mediator.Send(command with { Id = id }, cancellationToken);
        return NoContent();
    }

    [HttpPost("staff/{id:guid}/deactivate")]
    [RequirePermission(PermissionCodes.Settings.ManageStaff)]
    public async Task<IActionResult> DeactivateStaff(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeactivateStaffCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("staff/{id:guid}/reactivate")]
    [RequirePermission(PermissionCodes.Settings.ManageStaff)]
    public async Task<IActionResult> ReactivateStaff(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new ReactivateStaffCommand(id), cancellationToken);
        return NoContent();
    }

    // Returns the new password in the response body and nowhere else — there is no mail sender in
    // this product, so the manager reads it out. It is never persisted in the clear.
    [HttpPost("staff/{id:guid}/reset-password")]
    [RequirePermission(PermissionCodes.Settings.ManageStaff)]
    public async Task<ActionResult<ResetStaffPasswordResultDto>> ResetStaffPassword(Guid id, CancellationToken cancellationToken)
        => Ok(await mediator.Send(new ResetStaffPasswordCommand(id), cancellationToken));

    [HttpGet("audit-log")]
    [RequirePermission(PermissionCodes.Settings.View)]
    public async Task<ActionResult<PagedList<AuditLogDto>>> AuditLog(
        [FromQuery] string? entityType, [FromQuery] Guid? userId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
        => Ok(await mediator.Send(new GetAuditLogsQuery(entityType, userId, page, pageSize), cancellationToken));
}
