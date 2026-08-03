using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Migration.Dtos;
using GymOS.Application.Modules.Migration.EntityHandlers;
using MediatR;

namespace GymOS.Application.Modules.Migration.Queries;

/// <summary>Only the entity types with a registered IImportEntityHandler are returned — the
/// remaining ImportEntityType values (Membership, Attendance, Payment) require resolving a
/// reference to an already-existing entity (a member, a plan, an invoice) rather than a flat
/// create-from-row, which the current handler shape doesn't support yet.</summary>
public record GetImportEntitySchemasQuery : IQuery<List<ImportEntitySchemaDto>>;

public class GetImportEntitySchemasQueryHandler(IEnumerable<IImportEntityHandler> entityHandlers)
    : IRequestHandler<GetImportEntitySchemasQuery, List<ImportEntitySchemaDto>>
{
    public Task<List<ImportEntitySchemaDto>> Handle(GetImportEntitySchemasQuery request, CancellationToken cancellationToken)
        => Task.FromResult(entityHandlers
            .Select(h => new ImportEntitySchemaDto(h.EntityType, h.RequiredFields, h.OptionalFields))
            .ToList());
}
