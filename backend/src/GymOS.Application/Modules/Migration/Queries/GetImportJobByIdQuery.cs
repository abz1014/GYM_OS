using System.Text.Json;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Migration.Dtos;
using GymOS.Domain.Migration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Migration.Queries;

public record GetImportJobByIdQuery(Guid Id) : IQuery<ImportJobDetailDto>;

public class GetImportJobByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetImportJobByIdQuery, ImportJobDetailDto>
{
    public async Task<ImportJobDetailDto> Handle(GetImportJobByIdQuery request, CancellationToken cancellationToken)
    {
        var job = await db.ImportJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ImportJob), request.Id);

        var firstRow = await db.ImportRows.AsNoTracking()
            .Where(r => r.ImportJobId == request.Id)
            .OrderBy(r => r.RowNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var detectedColumns = firstRow is null
            ? []
            : (JsonSerializer.Deserialize<Dictionary<string, string>>(firstRow.RawDataJson) ?? []).Keys.ToList();

        var mappings = await db.ImportFieldMappings.AsNoTracking()
            .Where(m => m.ImportJobId == request.Id)
            .Select(m => new ImportFieldMappingDto(m.SourceColumnName, m.TargetFieldName))
            .ToListAsync(cancellationToken);

        return new ImportJobDetailDto(
            job.Id, job.EntityType, job.FileName, job.Status, job.TotalRows, job.ValidRows, job.DuplicateRows, job.ErrorRows,
            job.CreatedAt, job.CommittedAt, job.RolledBackAt, detectedColumns, mappings);
    }
}
