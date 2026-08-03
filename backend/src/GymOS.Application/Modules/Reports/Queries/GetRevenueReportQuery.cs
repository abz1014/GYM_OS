using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Reports.Dtos;
using GymOS.Domain.Billing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Reports.Queries;

public record GetRevenueReportQuery(int MonthsBack = 6) : IQuery<List<RevenueReportPointDto>>;

public class GetRevenueReportQueryHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetRevenueReportQuery, List<RevenueReportPointDto>>
{
    public async Task<List<RevenueReportPointDto>> Handle(GetRevenueReportQuery request, CancellationToken cancellationToken)
        => await BuildAsync(db, dateTimeProvider, request.MonthsBack, cancellationToken);

    internal static async Task<List<RevenueReportPointDto>> BuildAsync(
        IApplicationDbContext db, IDateTimeProvider dateTimeProvider, int monthsBack, CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.UtcNow;
        var cutoff = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(-(monthsBack - 1));

        var payments = await db.Payments.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Completed && p.PaidAt >= cutoff)
            .Select(p => new { p.PaidAt, p.Amount })
            .ToListAsync(cancellationToken);

        var buckets = new List<RevenueReportPointDto>();
        for (var i = monthsBack - 1; i >= 0; i--)
        {
            var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(-i);
            var monthEnd = monthStart.AddMonths(1);
            var total = payments.Where(p => p.PaidAt >= monthStart && p.PaidAt < monthEnd).Sum(p => p.Amount);
            buckets.Add(new RevenueReportPointDto(monthStart.ToString("MMM yyyy"), total));
        }

        return buckets;
    }
}
