using GymOS.Domain.Migration;
using MediatR;

namespace GymOS.Application.Modules.Migration.EntityHandlers;

public record ImportValidationResult(bool IsValid, string? Error, bool IsDuplicate)
{
    public static ImportValidationResult Ok() => new(true, null, false);
    public static ImportValidationResult Duplicate(string reason) => new(false, reason, true);
    public static ImportValidationResult Invalid(string reason) => new(false, reason, false);
}

/// <summary>
/// One implementation per supported ImportEntityType. Validate/Commit operate on the row's
/// already-mapped fields (source CSV column -> target field name, per ImportFieldMapping) so the
/// handler only ever deals with target field names.
/// </summary>
public interface IImportEntityHandler
{
    ImportEntityType EntityType { get; }

    IReadOnlyList<string> RequiredFields { get; }

    IReadOnlyList<string> OptionalFields { get; }

    /// <summary>The value used to detect duplicates both against the database and against other
    /// rows in the same file (e.g. the same email appearing twice in one CSV) — null if the row's
    /// required fields aren't present yet (already reported separately by ValidateAsync).</summary>
    string? GetNaturalKey(IReadOnlyDictionary<string, string> fields);

    Task<ImportValidationResult> ValidateAsync(IReadOnlyDictionary<string, string> fields, CancellationToken cancellationToken);

    Task<Guid> CommitAsync(IReadOnlyDictionary<string, string> fields, Guid branchId, ISender sender, CancellationToken cancellationToken);

    Task RollbackAsync(Guid mappedEntityId, CancellationToken cancellationToken);
}
