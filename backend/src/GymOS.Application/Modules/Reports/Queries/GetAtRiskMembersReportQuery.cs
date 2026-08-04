using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Reports.Dtos;
using GymOS.Domain.Members;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Reports.Queries;

/// <summary>
/// The actionable list behind the automated win-back job: paying (Active) members who've gone
/// quiet, longest-absent first, so staff can follow up personally instead of waiting on the
/// notification. Uses the exact same threshold as ChurnRiskPolicy — one definition of "at risk"
/// shared by the report and the automation, rather than two thresholds that could drift apart.
/// Members who've never visited are excluded here too: that's an onboarding gap, not churn.
/// </summary>
public record GetAtRiskMembersReportQuery : IQuery<List<AtRiskMemberRowDto>>;

public class GetAtRiskMembersReportQueryHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetAtRiskMembersReportQuery, List<AtRiskMemberRowDto>>
{
    public async Task<List<AtRiskMemberRowDto>> Handle(GetAtRiskMembersReportQuery request, CancellationToken cancellationToken)
        => await BuildAsync(db, dateTimeProvider, cancellationToken);

    internal static async Task<List<AtRiskMemberRowDto>> BuildAsync(
        IApplicationDbContext db, IDateTimeProvider dateTimeProvider, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);

        var activeMembers = await db.Members.AsNoTracking()
            .Where(m => m.Status == MemberStatus.Active)
            .Select(m => new { m.Id, m.FirstName, m.LastName, m.MemberCode })
            .ToListAsync(cancellationToken);

        var activeMemberIds = activeMembers.Select(m => m.Id).ToList();

        // Max(DateTimeOffset) doesn't translate on SQLite (the in-memory test provider) — pull each
        // active member's check-in timestamps and take the max client-side. Same "aggregate small
        // per-scope sets in memory" pattern already used for waitlist/roster ordering elsewhere.
        var checkInTimestamps = await db.AttendanceRecords.AsNoTracking()
            .Where(a => activeMemberIds.Contains(a.MemberId))
            .Select(a => new { a.MemberId, a.CheckInAt })
            .ToListAsync(cancellationToken);

        var lastCheckInByMemberId = checkInTimestamps
            .GroupBy(x => x.MemberId)
            .ToDictionary(g => g.Key, g => g.Max(x => x.CheckInAt));

        var rows = new List<AtRiskMemberRowDto>();
        foreach (var member in activeMembers)
        {
            if (!lastCheckInByMemberId.TryGetValue(member.Id, out var lastCheckInAt))
            {
                continue; // never visited — onboarding, not churn
            }

            var lastCheckInDate = DateOnly.FromDateTime(lastCheckInAt.UtcDateTime);
            var daysSince = today.DayNumber - lastCheckInDate.DayNumber;
            if (daysSince < ChurnRiskPolicy.InactivityThresholdDays)
            {
                continue;
            }

            rows.Add(new AtRiskMemberRowDto(member.Id, $"{member.FirstName} {member.LastName}", member.MemberCode, lastCheckInDate, daysSince));
        }

        return rows.OrderByDescending(r => r.DaysSinceLastVisit).ToList();
    }
}
