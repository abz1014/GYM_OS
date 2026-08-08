using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Reports.Dtos;
using GymOS.Domain.Members;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Reports.Queries;

/// <summary>
/// Week-N return: of the members who joined long enough ago to have reached week N, what share came
/// to the gym during it.
///
/// This is the outcome the other three gate metrics are proxies for. Capture rate says how well the
/// app observes training, time-to-log says whether it observes it in the moment, sessions-per-week
/// says how much there is to observe — none of them is why a gym pays for software. Someone coming
/// back in week 12 is.
///
/// It is deliberately separate from the cohort-retention report next door. That one asks how a
/// month's intake decays over the following year, for a gym owner looking at churn. This one asks a
/// fixed question at three fixed ages so the answer is comparable between gates: the roadmap's
/// week 2 / 4 / 12, computed the same way every time it is run.
///
/// Members whose week N has not finished are excluded from both halves of the fraction rather than
/// counted as not returning — see ReturnRatePolicy, which exists mostly to make that hard to get
/// wrong. Without it a gym signing new members quickly would show collapsing retention precisely
/// because it was growing.
/// </summary>
public record GetReturnRateReportQuery : IQuery<ReturnRateReportDto>;

public class GetReturnRateReportQueryHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetReturnRateReportQuery, ReturnRateReportDto>
{
    public async Task<ReturnRateReportDto> Handle(GetReturnRateReportQuery request, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);

        var joinDates = await db.Members.AsNoTracking()
            .Select(m => new { m.Id, m.JoinDate })
            .ToListAsync(cancellationToken);

        // Reduced to (member, day) in memory: a DateTimeOffset range filter doesn't translate on
        // SQLite, the same limitation every other query over this table works around. No window is
        // applied — week 12 of a member who joined a year ago is still week 12, and clipping to a
        // recent window would quietly drop the oldest cohorts, which are the only ones that have a
        // week-12 answer at all.
        var visitsByMember = (await db.AttendanceRecords.AsNoTracking()
                .Select(a => new { a.MemberId, a.CheckInAt })
                .ToListAsync(cancellationToken))
            .Select(a => (a.MemberId, Day: DateOnly.FromDateTime(a.CheckInAt.UtcDateTime)))
            .Distinct()
            .GroupBy(a => a.MemberId)
            .ToDictionary(g => g.Key, g => g.Select(a => a.Day).ToList());

        var points = new List<ReturnRatePointDto>();

        foreach (var week in ReturnRatePolicy.GateWeeks)
        {
            var eligible = joinDates.Where(m => ReturnRatePolicy.IsEligibleForWeek(m.JoinDate, today, week)).ToList();

            var returned = eligible.Count(m =>
                visitsByMember.TryGetValue(m.Id, out var days) &&
                ReturnRatePolicy.ReturnedInWeek(m.JoinDate, days, week));

            points.Add(new ReturnRatePointDto(
                week,
                eligible.Count,
                returned,
                ReturnRatePolicy.RatePercent(returned, eligible.Count)));
        }

        return new ReturnRateReportDto(today, points);
    }
}
