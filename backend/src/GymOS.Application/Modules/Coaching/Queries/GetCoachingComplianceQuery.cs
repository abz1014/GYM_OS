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
/// Workout and nutrition consistency for every active member across the caller's accessible
/// branches, so a trainer can scan a whole roster for who's drifting rather than checking one
/// member at a time. Both percentages come from CoachingPolicy; this query only assembles the date
/// lists each one needs.
/// </summary>
public record GetCoachingComplianceQuery : IQuery<List<ComplianceRowDto>>;

public class GetCoachingComplianceQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetCoachingComplianceQuery, List<ComplianceRowDto>>
{
    public async Task<List<ComplianceRowDto>> Handle(GetCoachingComplianceQuery request, CancellationToken cancellationToken)
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
        var windowStart = today.AddDays(-6); // trailing 7-day window, same shape as GetMyRecoveryQuery

        var workoutTimestampsByMember = (await db.WorkoutLogs.AsNoTracking()
                .Where(w => memberIds.Contains(w.MemberId))
                .Select(w => new { w.MemberId, w.LoggedAt })
                .ToListAsync(cancellationToken))
            .GroupBy(w => w.MemberId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.LoggedAt).ToList());

        var plansByMember = (await db.DietPlans.AsNoTracking()
                .Where(p => memberIds.Contains(p.MemberId))
                .Select(p => new { p.MemberId, p.StartDate, p.EndDate })
                .ToListAsync(cancellationToken))
            .GroupBy(p => p.MemberId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var mealTimestampsByMember = (await db.MealEntries.AsNoTracking()
                .Where(e => e.ConsumedAt != null && memberIds.Contains(e.DietPlan!.MemberId))
                .Select(e => new { MemberId = e.DietPlan!.MemberId, ConsumedAt = e.ConsumedAt!.Value })
                .ToListAsync(cancellationToken))
            .GroupBy(e => e.MemberId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.ConsumedAt).ToList());

        /*
         * The days members confirmed they stayed on plan.
         *
         * Counted alongside logged meals, not instead of them. The member's nutrition screen is a
         * prescription now rather than a food diary, so the meal rows this metric was built on are
         * about to stop arriving — and CoachingPolicy returns 0%, not null, for a member who HAS a
         * plan and logs nothing. Without this every trainer dashboard would have quietly asserted
         * that every member was ignoring their diet.
         *
         * OnDate is already a plain date in the gym's terms, so unlike the meal timestamps it needs
         * no conversion.
         */
        var adherenceDatesByMember = (await db.PlanAdherenceLogs.AsNoTracking()
                .Where(a => memberIds.Contains(a.MemberId) && a.OnDate >= windowStart)
                .Select(a => new { a.MemberId, a.OnDate })
                .ToListAsync(cancellationToken))
            .GroupBy(a => a.MemberId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.OnDate).ToList());

        var rows = new List<ComplianceRowDto>();
        foreach (var member in members)
        {
            workoutTimestampsByMember.TryGetValue(member.Id, out var workoutTimestamps);
            workoutTimestamps ??= [];
            var workoutDates = workoutTimestamps.Select(ToDate).Distinct().ToList();
            var workoutAdherence = CoachingPolicy.WorkoutAdherencePercent(workoutDates, today);

            plansByMember.TryGetValue(member.Id, out var plans);
            plans ??= [];
            var planActiveDatesInWindow = new List<DateOnly>();
            for (var d = windowStart; d <= today; d = d.AddDays(1))
            {
                if (plans.Any(p => p.StartDate <= d && (p.EndDate == null || p.EndDate >= d)))
                {
                    planActiveDatesInWindow.Add(d);
                }
            }

            mealTimestampsByMember.TryGetValue(member.Id, out var mealTimestamps);
            mealTimestamps ??= [];
            var mealDatesInWindow = mealTimestamps.Select(ToDate).Where(d => d >= windowStart && d <= today).Distinct().ToList();

            adherenceDatesByMember.TryGetValue(member.Id, out var adherenceDates);
            adherenceDates ??= [];

            var nutritionAdherence = CoachingPolicy.NutritionAdherencePercent(
                new CoachingPolicy.NutritionAdherenceSignals(
                    plans.Count > 0,
                    planActiveDatesInWindow,
                    mealDatesInWindow,
                    adherenceDates.Where(d => d >= windowStart && d <= today).Distinct().ToList()));

            rows.Add(new ComplianceRowDto(
                member.Id, $"{member.FirstName} {member.LastName}", member.MemberCode,
                workoutAdherence, nutritionAdherence,
                workoutTimestamps.Count > 0 ? workoutTimestamps.Max() : null,
                mealTimestamps.Count > 0 ? mealTimestamps.Max() : null));
        }

        // Lowest workout adherence first — the members most worth a trainer's attention surface at the top.
        return rows.OrderBy(r => r.WorkoutAdherencePercent).ThenBy(r => r.NutritionAdherencePercent ?? 0).ToList();
    }

    private static DateOnly ToDate(DateTimeOffset value) => DateOnly.FromDateTime(value.UtcDateTime);
}
