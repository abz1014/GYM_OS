using System.Text.Json;
using GymOS.Application.Common.Extensions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Migration.Dtos;
using GymOS.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Migration.Queries;

public record GetImportJobRowsQuery(Guid ImportJobId, int Page = 1, int PageSize = 50) : IQuery<PagedList<ImportRowDto>>;

public class GetImportJobRowsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetImportJobRowsQuery, PagedList<ImportRowDto>>
{
    public async Task<PagedList<ImportRowDto>> Handle(GetImportJobRowsQuery request, CancellationToken cancellationToken)
    {
        var paged = await db.ImportRows.AsNoTracking()
            .Where(r => r.ImportJobId == request.ImportJobId)
            .OrderBy(r => r.RowNumber)
            .ToPagedListAsync(request.Page, request.PageSize, cancellationToken);

        var items = paged.Items
            .Select(r => new ImportRowDto(
                r.Id, r.RowNumber, JsonSerializer.Deserialize<Dictionary<string, string>>(r.RawDataJson) ?? [],
                r.Status, r.ValidationErrors, r.IsDuplicate, r.MappedEntityId))
            .ToList();

        return new PagedList<ImportRowDto>(items, paged.Page, paged.PageSize, paged.TotalCount);
    }
}
