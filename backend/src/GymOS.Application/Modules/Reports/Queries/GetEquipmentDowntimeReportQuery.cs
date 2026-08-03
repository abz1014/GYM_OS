using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Reports.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Reports.Queries;

public record GetEquipmentDowntimeReportQuery(int MonthsBack = 6) : IQuery<List<EquipmentDowntimeReportRowDto>>;

public class GetEquipmentDowntimeReportQueryHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetEquipmentDowntimeReportQuery, List<EquipmentDowntimeReportRowDto>>
{
    public async Task<List<EquipmentDowntimeReportRowDto>> Handle(GetEquipmentDowntimeReportQuery request, CancellationToken cancellationToken)
        => await BuildAsync(db, dateTimeProvider, request.MonthsBack, cancellationToken);

    internal static async Task<List<EquipmentDowntimeReportRowDto>> BuildAsync(
        IApplicationDbContext db, IDateTimeProvider dateTimeProvider, int monthsBack, CancellationToken cancellationToken)
    {
        var cutoff = dateTimeProvider.UtcNow.AddMonths(-monthsBack);

        var assets = await db.Assets.AsNoTracking()
            .Select(a => new { a.Id, a.Name, a.AssetTag })
            .ToListAsync(cancellationToken);
        var assetIds = assets.Select(a => a.Id).ToHashSet();

        // DowntimeLog isn't tenant-scoped itself; restricting to the tenant's known Asset ids
        // scopes it the same way CommissionRecord is scoped through Trainers above.
        var downtime = (await db.DowntimeLogs.AsNoTracking()
                .Where(d => d.StartedAt >= cutoff)
                .Select(d => new { d.AssetId, d.StartedAt, d.EndedAt })
                .ToListAsync(cancellationToken))
            .Where(d => assetIds.Contains(d.AssetId))
            .ToList();

        var maintenanceCosts = await db.WorkOrders.AsNoTracking()
            .Where(w => w.Cost != null && w.CreatedAt >= cutoff)
            .Select(w => new { w.AssetId, Cost = w.Cost!.Value })
            .ToListAsync(cancellationToken);

        return assets
            .Where(a => downtime.Any(d => d.AssetId == a.Id) || maintenanceCosts.Any(c => c.AssetId == a.Id))
            .Select(a => new EquipmentDowntimeReportRowDto(
                a.Name,
                a.AssetTag,
                downtime.Count(d => d.AssetId == a.Id),
                downtime.Where(d => d.AssetId == a.Id && d.EndedAt != null).Sum(d => (d.EndedAt!.Value - d.StartedAt).TotalHours),
                maintenanceCosts.Where(c => c.AssetId == a.Id).Sum(c => c.Cost)))
            .OrderByDescending(r => r.TotalDowntimeHours)
            .ToList();
    }
}
