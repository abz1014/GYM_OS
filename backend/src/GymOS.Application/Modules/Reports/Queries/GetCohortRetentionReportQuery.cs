using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Reports.Dtos;
using GymOS.Domain.Members;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Reports.Queries;

/// <summary>
/// One retention number per monthly join cohort: of everyone who joined in that calendar month,
/// what fraction is still an Active member today? A join-month grid (visits at month 1, month 2...)
/// would answer a sharper question, but needs behavioral event history per cohort-age this schema
/// doesn't cheaply support yet; this single number per cohort already answers "are newer members
/// sticking better than older ones" — the question an owner is actually asking week to week.
/// </summary>
public record GetCohortRetentionReportQuery(int MonthsBack = 12) : IQuery<List<CohortRetentionPointDto>>;

public class GetCohortRetentionReportQueryHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetCohortRetentionReportQuery, List<CohortRetentionPointDto>>
{
    public async Task<List<CohortRetentionPointDto>> Handle(GetCohortRetentionReportQuery request, CancellationToken cancellationToken)
        => await BuildAsync(db, dateTimeProvider, request.MonthsBack, cancellationToken);

    internal static async Task<List<CohortRetentionPointDto>> BuildAsync(
        IApplicationDbContext db, IDateTimeProvider dateTimeProvider, int monthsBack, CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.UtcNow;
        var cutoff = new DateOnly(now.Year, now.Month, 1).AddMonths(-(monthsBack - 1));

        var members = await db.Members.AsNoTracking()
            .Where(m => m.JoinDate >= cutoff)
            .Select(m => new { m.JoinDate, m.Status })
            .ToListAsync(cancellationToken);

        var points = new List<CohortRetentionPointDto>();
        for (var i = monthsBack - 1; i >= 0; i--)
        {
            var monthStart = new DateOnly(now.Year, now.Month, 1).AddMonths(-i);
            var monthEnd = monthStart.AddMonths(1);

            var cohort = members.Where(m => m.JoinDate >= monthStart && m.JoinDate < monthEnd).ToList();
            var stillActive = cohort.Count(m => m.Status == MemberStatus.Active);
            var retentionRate = cohort.Count == 0 ? 0 : Math.Round(stillActive * 100.0 / cohort.Count, 1);

            points.Add(new CohortRetentionPointDto(monthStart.ToString("MMM yyyy"), cohort.Count, stillActive, retentionRate));
        }

        return points;
    }
}
