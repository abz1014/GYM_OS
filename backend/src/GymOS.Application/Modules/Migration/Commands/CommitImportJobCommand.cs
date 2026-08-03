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

public record CommitImportJobCommand(Guid ImportJobId, Guid BranchId) : ICommand<Unit>;

public class CommitImportJobCommandValidator : AbstractValidator<CommitImportJobCommand>
{
    public CommitImportJobCommandValidator()
    {
        RuleFor(x => x.ImportJobId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
    }
}

public class CommitImportJobCommandHandler(IApplicationDbContext db, ISender sender, IEnumerable<IImportEntityHandler> entityHandlers, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<CommitImportJobCommand, Unit>
{
    public async Task<Unit> Handle(CommitImportJobCommand request, CancellationToken cancellationToken)
    {
        var job = await db.ImportJobs.FirstOrDefaultAsync(j => j.Id == request.ImportJobId, cancellationToken)
            ?? throw new NotFoundException(nameof(ImportJob), request.ImportJobId);

        if (job.Status != ImportStatus.Validated)
        {
            throw new ValidationException("Validate the import before committing.");
        }

        var branchExists = await db.Branches.AnyAsync(b => b.Id == request.BranchId, cancellationToken);
        if (!branchExists)
        {
            throw new NotFoundException(nameof(Domain.Tenancy.Branch), request.BranchId);
        }

        var handler = entityHandlers.FirstOrDefault(h => h.EntityType == job.EntityType)
            ?? throw new ValidationException($"Importing '{job.EntityType}' is not yet supported.");

        var mappings = await db.ImportFieldMappings
            .Where(m => m.ImportJobId == request.ImportJobId)
            .ToDictionaryAsync(m => m.SourceColumnName, m => m.TargetFieldName, cancellationToken);

        var rows = await db.ImportRows.Where(r => r.ImportJobId == request.ImportJobId).ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            if (row.Status == ImportRowStatus.Valid && !row.IsDuplicate)
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

                var entityId = await handler.CommitAsync(mapped, request.BranchId, sender, cancellationToken);
                row.MappedEntityId = entityId;
                row.Status = ImportRowStatus.Committed;
            }
            else if (row.Status != ImportRowStatus.Committed)
            {
                row.Status = ImportRowStatus.Skipped;
            }
        }

        job.Status = ImportStatus.Completed;
        job.CommittedAt = dateTimeProvider.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
