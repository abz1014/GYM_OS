using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Reports.Dtos;
using GymOS.Domain.Trainers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Reports.Queries;

public record GetTrainerCommissionReportQuery(int MonthsBack = 6) : IQuery<List<TrainerCommissionReportRowDto>>;

public class GetTrainerCommissionReportQueryHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetTrainerCommissionReportQuery, List<TrainerCommissionReportRowDto>>
{
    public async Task<List<TrainerCommissionReportRowDto>> Handle(GetTrainerCommissionReportQuery request, CancellationToken cancellationToken)
        => await BuildAsync(db, dateTimeProvider, request.MonthsBack, cancellationToken);

    internal static async Task<List<TrainerCommissionReportRowDto>> BuildAsync(
        IApplicationDbContext db, IDateTimeProvider dateTimeProvider, int monthsBack, CancellationToken cancellationToken)
    {
        var cutoff = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime).AddMonths(-monthsBack);

        // CommissionRecord isn't tenant-scoped itself — joining through Trainers (which is)
        // naturally restricts results to the current tenant, the same pattern DowntimeLog and
        // StockMovement rely on below.
        var records = await db.CommissionRecords.AsNoTracking()
            .Where(c => c.Period >= cutoff)
            .Join(db.Trainers, c => c.TrainerId, t => t.Id, (c, t) => new { c.Amount, c.Status, t.UserId })
            .Join(db.Users, x => x.UserId, u => u.Id, (x, u) => new { x.Amount, x.Status, TrainerName = u.FirstName + " " + u.LastName })
            .ToListAsync(cancellationToken);

        return records
            .GroupBy(r => r.TrainerName)
            .Select(g => new TrainerCommissionReportRowDto(
                g.Key,
                g.Where(x => x.Status == CommissionStatus.Pending).Sum(x => x.Amount),
                g.Where(x => x.Status == CommissionStatus.Paid).Sum(x => x.Amount),
                g.Count()))
            .OrderByDescending(r => r.TotalPending + r.TotalPaid)
            .ToList();
    }
}
