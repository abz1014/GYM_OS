using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Migration.Dtos;
using GymOS.Application.Modules.Migration.EntityHandlers;
using MediatR;

namespace GymOS.Application.Modules.Migration.Queries;

/// <summary>Only the entity types with a registered IImportEntityHandler are returned — every
/// ImportEntityType currently has one, so this always returns all 8, but the shape stays dynamic
/// in case a future entity type ships without its handler yet.</summary>
public record GetImportEntitySchemasQuery : IQuery<List<ImportEntitySchemaDto>>;

public class GetImportEntitySchemasQueryHandler(IEnumerable<IImportEntityHandler> entityHandlers)
    : IRequestHandler<GetImportEntitySchemasQuery, List<ImportEntitySchemaDto>>
{
    public Task<List<ImportEntitySchemaDto>> Handle(GetImportEntitySchemasQuery request, CancellationToken cancellationToken)
        => Task.FromResult(entityHandlers
            .Select(h => new ImportEntitySchemaDto(h.EntityType, h.RequiredFields, h.OptionalFields))
            .ToList());
}
