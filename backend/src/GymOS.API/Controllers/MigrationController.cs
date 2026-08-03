using GymOS.API.Authorization;
using GymOS.Application.Modules.Migration.Commands;
using GymOS.Application.Modules.Migration.Dtos;
using GymOS.Application.Modules.Migration.Queries;
using GymOS.Domain.Migration;
using GymOS.Shared;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymOS.API.Controllers;

[ApiController]
[Authorize]
[Route("api/migration/jobs")]
public class MigrationController(ISender mediator) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.Migration.Manage)]
    public async Task<ActionResult<List<ImportJobListItemDto>>> List(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetImportJobsQuery(), cancellationToken));

    [HttpGet("entity-schemas")]
    [RequirePermission(PermissionCodes.Migration.Manage)]
    public async Task<ActionResult<List<ImportEntitySchemaDto>>> EntitySchemas(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetImportEntitySchemasQuery(), cancellationToken));

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCodes.Migration.Manage)]
    public async Task<ActionResult<ImportJobDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetImportJobByIdQuery(id), cancellationToken));

    [HttpGet("{id:guid}/rows")]
    [RequirePermission(PermissionCodes.Migration.Manage)]
    public async Task<ActionResult<PagedList<ImportRowDto>>> Rows(
        Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
        => Ok(await mediator.Send(new GetImportJobRowsQuery(id, page, pageSize), cancellationToken));

    [HttpPost]
    [RequestSizeLimit(20_000_000)]
    [RequirePermission(PermissionCodes.Migration.Manage)]
    public async Task<ActionResult<ImportJobDetailDto>> Upload(
        [FromForm] ImportEntityType entityType, [FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(file.OpenReadStream());
        var content = await reader.ReadToEndAsync(cancellationToken);

        var result = await mediator.Send(new UploadImportJobCommand(entityType, file.FileName, content), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}/field-mappings")]
    [RequirePermission(PermissionCodes.Migration.Manage)]
    public async Task<IActionResult> SetFieldMappings(Guid id, SetImportFieldMappingsCommand command, CancellationToken cancellationToken)
    {
        await mediator.Send(command with { ImportJobId = id }, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/validate")]
    [RequirePermission(PermissionCodes.Migration.Manage)]
    public async Task<IActionResult> Validate(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new ValidateImportJobCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/commit")]
    [RequirePermission(PermissionCodes.Migration.Manage)]
    public async Task<IActionResult> Commit(Guid id, CommitImportJobCommand command, CancellationToken cancellationToken)
    {
        await mediator.Send(command with { ImportJobId = id }, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/rollback")]
    [RequirePermission(PermissionCodes.Migration.Manage)]
    public async Task<IActionResult> Rollback(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new RollbackImportJobCommand(id), cancellationToken);
        return NoContent();
    }
}
