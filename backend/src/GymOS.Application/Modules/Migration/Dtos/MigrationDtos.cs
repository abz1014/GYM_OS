using GymOS.Domain.Migration;

namespace GymOS.Application.Modules.Migration.Dtos;

public record ImportFieldMappingDto(string SourceColumnName, string TargetFieldName);

public record ImportJobListItemDto(
    Guid Id, ImportEntityType EntityType, string FileName, ImportStatus Status,
    int TotalRows, int ValidRows, int DuplicateRows, int ErrorRows,
    DateTimeOffset CreatedAt, DateTimeOffset? CommittedAt, DateTimeOffset? RolledBackAt);

public record ImportJobDetailDto(
    Guid Id, ImportEntityType EntityType, string FileName, ImportStatus Status,
    int TotalRows, int ValidRows, int DuplicateRows, int ErrorRows,
    DateTimeOffset CreatedAt, DateTimeOffset? CommittedAt, DateTimeOffset? RolledBackAt,
    IReadOnlyList<string> DetectedColumns, IReadOnlyList<ImportFieldMappingDto> FieldMappings);

public record ImportRowDto(
    Guid Id, int RowNumber, IReadOnlyDictionary<string, string> Data, ImportRowStatus Status,
    string? ValidationErrors, bool IsDuplicate, Guid? MappedEntityId);

public record ImportEntitySchemaDto(ImportEntityType EntityType, IReadOnlyList<string> RequiredFields, IReadOnlyList<string> OptionalFields);
