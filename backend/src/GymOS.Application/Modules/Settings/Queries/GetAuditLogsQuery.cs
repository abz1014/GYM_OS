using GymOS.Application.Common.Extensions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Settings.Dtos;
using GymOS.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Settings.Queries;

public record GetAuditLogsQuery(string? EntityType, Guid? UserId, int Page = 1, int PageSize = 50) : IQuery<PagedList<AuditLogDto>>;

public class GetAuditLogsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetAuditLogsQuery, PagedList<AuditLogDto>>
{
    public async Task<PagedList<AuditLogDto>> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var query = db.AuditLogs.AsNoTracking()
            .Include(a => a.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.EntityType))
        {
            query = query.Where(a => a.EntityType == request.EntityType);
        }

        if (request.UserId is not null)
        {
            query = query.Where(a => a.UserId == request.UserId);
        }

        var paged = await query
            .OrderByDescending(a => a.OccurredAt)
            .Select(a => new AuditLogDto(
                a.Id, a.Action, a.EntityType, a.EntityId, a.UserId,
                a.User != null ? $"{a.User.FirstName} {a.User.LastName}".Trim() : null,
                a.DataAfter, a.OccurredAt))
            .ToPagedListAsync(request.Page, request.PageSize, cancellationToken);

        return paged;
    }
}
