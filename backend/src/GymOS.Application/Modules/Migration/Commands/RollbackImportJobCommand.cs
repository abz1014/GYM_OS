using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Migration.EntityHandlers;
using GymOS.Domain.Migration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Migration.Commands;

public record RollbackImportJobCommand(Guid ImportJobId) : ICommand<Unit>;

public class RollbackImportJobCommandValidator : AbstractValidator<RollbackImportJobCommand>
{
    public RollbackImportJobCommandValidator() => RuleFor(x => x.ImportJobId).NotEmpty();
}

public class RollbackImportJobCommandHandler(IApplicationDbContext db, IEnumerable<IImportEntityHandler> entityHandlers, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<RollbackImportJobCommand, Unit>
{
    public async Task<Unit> Handle(RollbackImportJobCommand request, CancellationToken cancellationToken)
    {
        var job = await db.ImportJobs.FirstOrDefaultAsync(j => j.Id == request.ImportJobId, cancellationToken)
            ?? throw new NotFoundException(nameof(ImportJob), request.ImportJobId);

        if (job.Status != ImportStatus.Completed)
        {
            throw new ValidationException("Only a completed import can be rolled back.");
        }

        var handler = entityHandlers.FirstOrDefault(h => h.EntityType == job.EntityType)
            ?? throw new ValidationException($"Importing '{job.EntityType}' is not yet supported.");

        var committedRows = await db.ImportRows
            .Where(r => r.ImportJobId == request.ImportJobId && r.Status == ImportRowStatus.Committed && r.MappedEntityId != null)
            .ToListAsync(cancellationToken);

        foreach (var row in committedRows)
        {
            await handler.RollbackAsync(row.MappedEntityId!.Value, cancellationToken);
        }

        job.Status = ImportStatus.RolledBack;
        job.RolledBackAt = dateTimeProvider.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
