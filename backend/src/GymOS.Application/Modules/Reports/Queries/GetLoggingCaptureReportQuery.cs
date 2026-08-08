using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Reports.Dtos;
using GymOS.Domain.Members;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Reports.Queries;

/// <summary>
/// How much of what happens in the gym the app actually captures — the share of visits that produced
/// a recorded workout, week by week.
///
/// This exists because everything GymOS gives a member back (XP, records, mastery, streaks,
/// leaderboards) can only be computed from sessions that were recorded. A gym where people train
/// hard and log nothing has an engine running on air, and no other report would show it: attendance
/// looks healthy and workout reports look thin, but nothing puts the two side by side.
///
/// Needs no new data and no setup, so it works for any gym from day one — attendance and workout
/// logs are both already there. The definition lives in CaptureRatePolicy so the report, the member
/// surfaces and any later analysis all count a "session" the same way.
/// </summary>
public record GetLoggingCaptureReportQuery(int WeeksBack = 12) : IQuery<LoggingCaptureReportDto>;

public class GetLoggingCaptureReportQueryHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetLoggingCaptureReportQuery, LoggingCaptureReportDto>
{
    public async Task<LoggingCaptureReportDto> Handle(GetLoggingCaptureReportQuery request, CancellationToken cancellationToken)
    {
        var weeks = Math.Clamp(request.WeeksBack, 1, 52);
        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);
        // Monday-start weeks, matching StreakCalculator — every "week" in this product is the same week.
        var firstWeekStart = StreakCalculator.WeekStart(today).AddDays(-7 * (weeks - 1));
        var lastWeekEnd = firstWeekStart.AddDays(7 * weeks - 1);

        // Both sides are reduced to (member, day) pairs and windowed in memory. A DateTimeOffset range
        // filter doesn't translate on SQLite (the in-memory test provider) — the same limitation every
        // other query over these tables works around, and the reason none of them filter dates in SQL.
        var visitDays = (await db.AttendanceRecords.AsNoTracking()
                .Select(a => new { a.MemberId, a.CheckInAt })
                .ToListAsync(cancellationToken))
            .Select(a => (a.MemberId, Day: DateOnly.FromDateTime(a.CheckInAt.UtcDateTime)))
            .Where(a => a.Day >= firstWeekStart && a.Day <= lastWeekEnd)
            .Distinct()
            .ToList();

        var logDays = (await db.WorkoutLogs.AsNoTracking()
                .Select(w => new { w.MemberId, w.LoggedAt })
                .ToListAsync(cancellationToken))
            .Select(w => (w.MemberId, Day: DateOnly.FromDateTime(w.LoggedAt.UtcDateTime)))
            .Where(w => w.Day >= firstWeekStart && w.Day <= lastWeekEnd)
            .Distinct()
            .ToHashSet();

        var visitSet = visitDays.ToHashSet();

        /*
         * Time-to-log needs the clock, not just the calendar, so it reloads both sides with their
         * timestamps rather than reusing the distinct (member, day) sets above — those deliberately
         * collapse a day's rows to one and the earliest arrival is exactly what got collapsed away.
         * Earliest check-in and earliest log per member-day: a member who scans twice is measured
         * from when they first arrived, and one who logs three exercises separately is measured to
         * the first record rather than to whenever they finished typing.
         */
        var firstCheckInPerDay = (await db.AttendanceRecords.AsNoTracking()
                .Select(a => new { a.MemberId, a.CheckInAt })
                .ToListAsync(cancellationToken))
            .Select(a => new { a.MemberId, Day = DateOnly.FromDateTime(a.CheckInAt.UtcDateTime), a.CheckInAt })
            .Where(a => a.Day >= firstWeekStart && a.Day <= lastWeekEnd)
            .GroupBy(a => (a.MemberId, a.Day))
            .ToDictionary(g => g.Key, g => g.Min(a => a.CheckInAt));

        var firstLogPerDay = (await db.WorkoutLogs.AsNoTracking()
                .Select(w => new { w.MemberId, w.LoggedAt })
                .ToListAsync(cancellationToken))
            .Select(w => new { w.MemberId, Day = DateOnly.FromDateTime(w.LoggedAt.UtcDateTime), w.LoggedAt })
            .Where(w => w.Day >= firstWeekStart && w.Day <= lastWeekEnd)
            .GroupBy(w => (w.MemberId, w.Day))
            .ToDictionary(g => g.Key, g => g.Min(w => w.LoggedAt));

        var latencies = new List<int>();
        var buckets = new Dictionary<LogLatencyBucket, int>();

        foreach (var (key, checkInAt) in firstCheckInPerDay)
        {
            // A visit is timed against that member's earliest log on the SAME day. Pairing across
            // days would mean guessing which visit a later record belonged to, and a wrong guess
            // silently invents a latency.
            if (!firstLogPerDay.TryGetValue(key, out var loggedAt) || !TimeToLogPolicy.IsMeasurable(checkInAt, loggedAt))
            {
                continue;
            }

            var minutes = (int)Math.Round((loggedAt - checkInAt).TotalMinutes, MidpointRounding.AwayFromZero);
            latencies.Add(minutes);

            var bucket = TimeToLogPolicy.Bucket(key.Day, DateOnly.FromDateTime(loggedAt.UtcDateTime), minutes);
            buckets[bucket] = buckets.GetValueOrDefault(bucket) + 1;
        }

        // Every bucket is emitted, including empty ones: "no sessions were recorded two days late"
        // is a result worth seeing, and a bucket that vanishes reads as a rendering bug.
        var latencyBuckets = Enum.GetValues<LogLatencyBucket>()
            .Select(b => new LogLatencyBucketDto(b.ToString(), buckets.GetValueOrDefault(b)))
            .ToList();

        var points = new List<CaptureRatePointDto>();
        for (var i = 0; i < weeks; i++)
        {
            var start = firstWeekStart.AddDays(7 * i);
            var end = start.AddDays(6);

            var visitsThisWeek = visitDays.Where(v => v.Day >= start && v.Day <= end).ToList();
            var logged = visitsThisWeek.Count(v => logDays.Contains(v));
            var orphans = logDays.Count(l => l.Day >= start && l.Day <= end && !visitSet.Contains(l));

            var point = new CapturePoint(start, visitsThisWeek.Count, logged, orphans);
            points.Add(new CaptureRatePointDto(start, point.VisitDays, point.LoggedVisitDays, point.OrphanLogDays, point.CaptureRatePercent));
        }

        var totalVisits = points.Sum(p => p.VisitDays);
        var totalLogged = points.Sum(p => p.LoggedVisitDays);
        var totalOrphans = points.Sum(p => p.OrphanLogDays);

        // Members who show up but never record anything: the population the one-tap logging work is
        // aimed at, and the most useful thing on this report for a gym owner to act on.
        var membersWhoVisited = visitDays.Select(v => v.MemberId).Distinct().ToHashSet();
        var membersWhoLogged = logDays.Select(l => l.MemberId).Distinct().ToHashSet();

        return new LoggingCaptureReportDto(
            firstWeekStart,
            today,
            totalVisits,
            totalLogged,
            totalOrphans,
            CaptureRatePolicy.RatePercent(totalLogged, totalVisits),
            CaptureRatePolicy.IsReliable(totalOrphans, totalLogged),
            membersWhoVisited.Count,
            membersWhoLogged.Count,
            membersWhoVisited.Except(membersWhoLogged).Count(),
            points,
            TimeToLogPolicy.MedianMinutes(latencies),
            latencyBuckets,
            // Guards the capture rate above it: a ratio that climbs while this falls means
            // confirmation got easier, not that anyone trained more. See ReturnRatePolicy.
            ReturnRatePolicy.SessionsPerMemberPerWeek(totalLogged, membersWhoVisited.Count, weeks));
    }
}
