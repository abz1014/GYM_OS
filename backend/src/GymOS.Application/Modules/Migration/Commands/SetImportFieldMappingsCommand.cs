using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Migration.Dtos;
using GymOS.Domain.Migration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Migration.Commands;

public record SetImportFieldMappingsCommand(Guid ImportJobId, IReadOnlyList<ImportFieldMappingDto> Mappings) : ICommand<Unit>;

public class SetImportFieldMappingsCommandValidator : AbstractValidator<SetImportFieldMappingsCommand>
{
    public SetImportFieldMappingsCommandValidator() => RuleFor(x => x.ImportJobId).NotEmpty();
}

public class SetImportFieldMappingsCommandHandler(IApplicationDbContext db) : IRequestHandler<SetImportFieldMappingsCommand, Unit>
{
    public async Task<Unit> Handle(SetImportFieldMappingsCommand request, CancellationToken cancellationToken)
    {
        var jobExists = await db.ImportJobs.AnyAsync(j => j.Id == request.ImportJobId, cancellationToken);
        if (!jobExists)
        {
            throw new NotFoundException(nameof(ImportJob), request.ImportJobId);
        }

        var existingMappings = await db.ImportFieldMappings.Where(m => m.ImportJobId == request.ImportJobId).ToListAsync(cancellationToken);
        db.ImportFieldMappings.RemoveRange(existingMappings);

        foreach (var mapping in request.Mappings)
        {
            db.ImportFieldMappings.Add(new ImportFieldMapping
            {
                ImportJobId = request.ImportJobId,
                SourceColumnName = mapping.SourceColumnName,
                TargetFieldName = mapping.TargetFieldName
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
