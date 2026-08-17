using GymOS.API.Authorization;
using GymOS.Application.Modules.Members.Commands;
using GymOS.Application.Modules.Members.Dtos;
using GymOS.Application.Modules.Members.Queries;
using GymOS.Domain.Members;
using GymOS.Shared;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymOS.API.Controllers;

[ApiController]
[Authorize]
[Route("api/members")]
public class MembersController(ISender mediator) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.Members.View)]
    public async Task<ActionResult<PagedList<MemberListItemDto>>> List(
        [FromQuery] string? searchTerm, [FromQuery] MemberStatus? status, [FromQuery] Guid? branchId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        => Ok(await mediator.Send(new GetMembersListQuery(searchTerm, status, branchId, page, pageSize), cancellationToken));

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCodes.Members.View)]
    public async Task<ActionResult<MemberDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetMemberByIdQuery(id), cancellationToken));

    /// <summary>
    /// One member's history across every module, newest first. members.view is the floor to reach it;
    /// each source inside is gated on its own module permission, so this returns strictly less than
    /// the caller could already read one screen at a time, never more.
    /// </summary>
    [HttpGet("{id:guid}/timeline")]
    [RequirePermission(PermissionCodes.Members.View)]
    public async Task<ActionResult<IReadOnlyList<MemberTimelineEntryDto>>> Timeline(
        Guid id, [FromQuery] int? take, CancellationToken cancellationToken)
        => Ok(await mediator.Send(
            take is null ? new GetMemberTimelineQuery(id) : new GetMemberTimelineQuery(id, take.Value),
            cancellationToken));

    [HttpPost]
    [RequirePermission(PermissionCodes.Members.Create)]
    public async Task<ActionResult<CreateMemberResultDto>> Create(CreateMemberCommand command, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// One button that always leaves the member able to sign in: issues a first login for a member
    /// who has never had one, or a fresh temporary password for a member who has lost theirs. See
    /// ProvisionMemberLoginCommand for which of the two actually happens.
    /// </summary>
    [HttpPost("{id:guid}/login")]
    [RequirePermission(PermissionCodes.Members.Update)]
    public async Task<ActionResult<CreateMemberResultDto>> ProvisionLogin(Guid id, CancellationToken cancellationToken)
        => Ok(await mediator.Send(new ProvisionMemberLoginCommand(id), cancellationToken));

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCodes.Members.Update)]
    public async Task<IActionResult> Update(Guid id, UpdateMemberCommand command, CancellationToken cancellationToken)
    {
        if (id != command.Id)
        {
            return BadRequest("Route id and body id must match.");
        }

        await mediator.Send(command, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/emergency-contacts")]
    [RequirePermission(PermissionCodes.Members.Update)]
    public async Task<ActionResult<Guid>> AddEmergencyContact(Guid id, AddEmergencyContactCommand command, CancellationToken cancellationToken)
        => Ok(await mediator.Send(command with { MemberId = id }, cancellationToken));

    [HttpPost("{id:guid}/medical-notes")]
    [RequirePermission(PermissionCodes.Members.Update)]
    public async Task<ActionResult<Guid>> AddMedicalNote(Guid id, AddMedicalNoteCommand command, CancellationToken cancellationToken)
        => Ok(await mediator.Send(command with { MemberId = id }, cancellationToken));

    [HttpPost("{id:guid}/measurements")]
    [RequirePermission(PermissionCodes.Members.Update)]
    public async Task<ActionResult<Guid>> AddMeasurement(Guid id, AddMeasurementCommand command, CancellationToken cancellationToken)
        => Ok(await mediator.Send(command with { MemberId = id }, cancellationToken));

    [HttpPost("{id:guid}/progress-photos")]
    [RequirePermission(PermissionCodes.Members.Update)]
    public async Task<ActionResult<Guid>> AddProgressPhoto(Guid id, AddProgressPhotoCommand command, CancellationToken cancellationToken)
        => Ok(await mediator.Send(command with { MemberId = id }, cancellationToken));

    [HttpPost("{id:guid}/memberships")]
    [RequirePermission(PermissionCodes.Members.ManageMembership)]
    public async Task<ActionResult<Guid>> Renew(Guid id, RenewMembershipCommand command, CancellationToken cancellationToken)
        => Ok(await mediator.Send(command with { MemberId = id }, cancellationToken));

    [HttpPost("memberships/{memberMembershipId:guid}/freeze")]
    [RequirePermission(PermissionCodes.Members.ManageMembership)]
    public async Task<IActionResult> Freeze(Guid memberMembershipId, FreezeMembershipCommand command, CancellationToken cancellationToken)
    {
        await mediator.Send(command with { MemberMembershipId = memberMembershipId }, cancellationToken);
        return NoContent();
    }

    /*
     * The two batch actions behind the members list's selection bar.
     *
     * Both return 200 with a per-member result rather than 204, and both return 200 even when every
     * member failed. That is deliberate: "freeze these twenty" is twenty independent decisions, and
     * the HTTP status describes whether the batch RAN, not whether every row inside it succeeded.
     * Collapsing a mixed result into 400 would leave the caller unable to tell which members were
     * changed — the exact ambiguity that makes bulk actions dangerous. See BatchResultDto.
     *
     * They carry the same permissions as their single-record equivalents, because a bulk action is
     * not a different capability from doing the same thing repeatedly by hand.
     */
    [HttpPost("batch/freeze")]
    [RequirePermission(PermissionCodes.Members.ManageMembership)]
    public async Task<ActionResult<BatchResultDto>> BatchFreeze(
        BatchFreezeMembershipsCommand command, CancellationToken cancellationToken)
        => Ok(await mediator.Send(command, cancellationToken));

    [HttpPost("batch/notes")]
    [RequirePermission(PermissionCodes.Members.Update)]
    public async Task<ActionResult<BatchResultDto>> BatchAddNote(
        BatchAddMemberNoteCommand command, CancellationToken cancellationToken)
        => Ok(await mediator.Send(command, cancellationToken));

    [HttpPost("memberships/{memberMembershipId:guid}/resume")]
    [RequirePermission(PermissionCodes.Members.ManageMembership)]
    public async Task<IActionResult> Resume(Guid memberMembershipId, CancellationToken cancellationToken)
    {
        await mediator.Send(new ResumeMembershipCommand(memberMembershipId), cancellationToken);
        return NoContent();
    }

    [HttpPost("memberships/{memberMembershipId:guid}/cancel")]
    [RequirePermission(PermissionCodes.Members.ManageMembership)]
    public async Task<IActionResult> Cancel(Guid memberMembershipId, CancelMembershipCommand command, CancellationToken cancellationToken)
    {
        await mediator.Send(command with { MemberMembershipId = memberMembershipId }, cancellationToken);
        return NoContent();
    }

    [HttpPost("memberships/{memberMembershipId:guid}/reactivate")]
    [RequirePermission(PermissionCodes.Members.ManageMembership)]
    public async Task<IActionResult> Reactivate(Guid memberMembershipId, CancellationToken cancellationToken)
    {
        await mediator.Send(new ReactivateMembershipCommand(memberMembershipId), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/transfer")]
    [RequirePermission(PermissionCodes.Members.ManageMembership)]
    public async Task<IActionResult> Transfer(Guid id, TransferMemberCommand command, CancellationToken cancellationToken)
    {
        await mediator.Send(command with { MemberId = id }, cancellationToken);
        return NoContent();
    }
}
