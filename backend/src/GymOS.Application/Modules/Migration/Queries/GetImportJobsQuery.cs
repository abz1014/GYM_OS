using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Migration.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Migration.Queries;

public record GetImportJobsQuery : IQuery<List<ImportJobListItemDto>>;

public class GetImportJobsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetImportJobsQuery, List<ImportJobListItemDto>>
{
    public Task<List<ImportJobListItemDto>> Handle(GetImportJobsQuery request, CancellationToken cancellationToken)
        => db.ImportJobs.AsNoTracking()
            .OrderByDescending(j => j.CreatedAt)
            .Select(j => new ImportJobListItemDto(
                j.Id, j.EntityType, j.FileName, j.Status, j.TotalRows, j.ValidRows, j.DuplicateRows, j.ErrorRows,
                j.CreatedAt, j.CommittedAt, j.RolledBackAt))
            .ToListAsync(cancellationToken);
}
