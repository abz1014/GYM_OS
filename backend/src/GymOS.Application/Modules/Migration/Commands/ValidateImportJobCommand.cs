using System.Text.Json;
using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Migration.EntityHandlers;
using GymOS.Domain.Migration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Migration.Commands;

public record ValidateImportJobCommand(Guid ImportJobId) : ICommand<Unit>;

public class ValidateImportJobCommandValidator : AbstractValidator<ValidateImportJobCommand>
{
    public ValidateImportJobCommandValidator() => RuleFor(x => x.ImportJobId).NotEmpty();
}

public class ValidateImportJobCommandHandler(IApplicationDbContext db, IEnumerable<IImportEntityHandler> entityHandlers)
    : IRequestHandler<ValidateImportJobCommand, Unit>
{
    public async Task<Unit> Handle(ValidateImportJobCommand request, CancellationToken cancellationToken)
    {
        var job = await db.ImportJobs.FirstOrDefaultAsync(j => j.Id == request.ImportJobId, cancellationToken)
            ?? throw new NotFoundException(nameof(ImportJob), request.ImportJobId);

        var handler = entityHandlers.FirstOrDefault(h => h.EntityType == job.EntityType)
            ?? throw new ValidationException($"Importing '{job.EntityType}' is not yet supported.");

        var mappings = await db.ImportFieldMappings
            .Where(m => m.ImportJobId == request.ImportJobId)
            .ToDictionaryAsync(m => m.SourceColumnName, m => m.TargetFieldName, cancellationToken);

        if (mappings.Count == 0)
        {
            throw new ValidationException("Set field mappings before validating.");
        }

        var rows = await db.ImportRows.Where(r => r.ImportJobId == request.ImportJobId).OrderBy(r => r.RowNumber).ToListAsync(cancellationToken);

        var validCount = 0;
        var duplicateCount = 0;
        var errorCount = 0;

        // Tracks natural keys already accepted earlier in this same file, so a repeated email/SKU/name
        // within one CSV is caught even though neither occurrence exists in the database yet.
        var seenKeysInBatch = new HashSet<string>();

        foreach (var row in rows)
        {
            var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(row.RawDataJson) ?? [];
            var mapped = new Dictionary<string, string>();

            foreach (var (source, target) in mappings)
            {
                if (raw.TryGetValue(source, out var value))
                {
                    mapped[target] = value;
                }
            }

            var result = await handler.ValidateAsync(mapped, cancellationToken);
            var naturalKey = handler.GetNaturalKey(mapped);

            if (result.IsValid && naturalKey is not null && !seenKeysInBatch.Add(naturalKey))
            {
                result = ImportValidationResult.Duplicate($"Duplicate of an earlier row in this same file ('{naturalKey}').");
            }

            row.IsDuplicate = result.IsDuplicate;
            row.ValidationErrors = result.Error;

            if (result.IsValid)
            {
                row.Status = ImportRowStatus.Valid;
                validCount++;
            }
            else
            {
                row.Status = ImportRowStatus.Invalid;
                if (result.IsDuplicate)
                {
                    duplicateCount++;
                }
                else
                {
                    errorCount++;
                }
            }
        }

        job.TotalRows = rows.Count;
        job.ValidRows = validCount;
        job.DuplicateRows = duplicateCount;
        job.ErrorRows = errorCount;
        job.Status = ImportStatus.Validated;

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
