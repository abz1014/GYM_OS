using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Common.Security;
using GymOS.Application.Modules.Coaching.Dtos;
using GymOS.Domain.Experience;
using GymOS.Domain.Members;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Coaching.Queries;

/// <summary>
/// Members across the caller's accessible branches worth a trainer checking in on: overtraining risk
/// (RecoveryPolicy, same rule as the member's own recovery banner) and a weekly streak that's alive
/// only on this week's grace period (CoachingPolicy.IsStreakBreakImminent) — a member can appear
/// under either or both.
/// </summary>
public record GetCoachingRisksQuery : IQuery<List<RiskRowDto>>;

public class GetCoachingRisksQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetCoachingRisksQuery, List<RiskRowDto>>
{
    public async Task<List<RiskRowDto>> Handle(GetCoachingRisksQuery request, CancellationToken cancellationToken)
    {
        var accessibleBranchIds = await BranchAccessResolver.GetAccessibleBranchIdsAsync(db, currentUser, cancellationToken);

        var members = await db.Members.AsNoTracking()
            .Where(m => accessibleBranchIds.Contains(m.BranchId) && m.Status == MemberStatus.Active)
            .Select(m => new { m.Id, m.FirstName, m.LastName, m.MemberCode })
            .ToListAsync(cancellationToken);
        if (members.Count == 0)
        {
            return [];
        }

        var memberIds = members.Select(m => m.Id).ToList();
        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);
        var windowStart = today.AddDays(-6);

        var workoutDatesByMember = (await db.WorkoutLogs.AsNoTracking()
                .Where(w => memberIds.Contains(w.MemberId))
                .Select(w => new { w.MemberId, w.LoggedAt })
                .ToListAsync(cancellationToken))
            .GroupBy(w => w.MemberId)
            .ToDictionary(g => g.Key, g => g.Select(x => ToDate(x.LoggedAt)).Distinct().ToList());

        var restDatesByMember = (await db.RecoveryLogs.AsNoTracking()
                .Where(r => memberIds.Contains(r.MemberId))
                .Select(r => new { r.MemberId, r.LoggedOn })
                .ToListAsync(cancellationToken))
            .GroupBy(r => r.MemberId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.LoggedOn).Distinct().ToList());

        var checkInDatesByMember = (await db.AttendanceRecords.AsNoTracking()
                .Where(a => memberIds.Contains(a.MemberId))
                .Select(a => new { a.MemberId, a.CheckInAt })
                .ToListAsync(cancellationToken))
            .GroupBy(a => a.MemberId)
            .ToDictionary(g => g.Key, g => g.Select(x => ToDate(x.CheckInAt)).Distinct().ToList());

        var thisWeekStart = StreakCalculator.WeekStart(today);
        var rows = new List<RiskRowDto>();

        foreach (var member in members)
        {
            workoutDatesByMember.TryGetValue(member.Id, out var workoutDates);
            workoutDates ??= [];
            restDatesByMember.TryGetValue(member.Id, out var restDates);
            restDates ??= [];

            var sessionsLast7 = workoutDates.Count(d => d >= windowStart);
            var restLast7 = restDates.Count(d => d >= windowStart);
            int? daysSinceLastWorkout = workoutDates.Count == 0 ? null : today.DayNumber - workoutDates.Max().DayNumber;

            var (status, reason) = RecoveryPolicy.ClassifyOverall(new RecoveryPolicy.RecoverySignals(sessionsLast7, restLast7, daysSinceLastWorkout));
            if (status == RecoveryStatus.OvertrainingRisk)
            {
                rows.Add(new RiskRowDto(member.Id, $"{member.FirstName} {member.LastName}", member.MemberCode, "OvertrainingRisk", reason));
            }

            checkInDatesByMember.TryGetValue(member.Id, out var checkInDates);
            checkInDates ??= [];
            var currentStreak = StreakCalculator.CurrentWeeklyStreak(checkInDates, today);
            var visitedThisWeek = checkInDates.Any(d => d >= thisWeekStart);
            if (CoachingPolicy.IsStreakBreakImminent(visitedThisWeek, currentStreak))
            {
                var weekWord = currentStreak == 1 ? "week" : "weeks";
                rows.Add(new RiskRowDto(
                    member.Id, $"{member.FirstName} {member.LastName}", member.MemberCode, "StreakBreakImminent",
                    $"{currentStreak}-{weekWord} check-in streak — hasn't been in yet this week."));
            }
        }

        return rows.OrderBy(r => r.MemberName).ToList();
    }

    private static DateOnly ToDate(DateTimeOffset value) => DateOnly.FromDateTime(value.UtcDateTime);
}
