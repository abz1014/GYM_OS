using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Experience.Dtos;
using GymOS.Application.Modules.Portal;
using GymOS.Domain.Common;
using GymOS.Domain.Members;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Experience.Queries;

/// <summary>
/// The member's three habit streaks in one round trip — attendance, workouts, and nutrition — each
/// the count of consecutive weeks with activity, computed by the same pure StreakCalculator the
/// progress page already uses (generalised here from check-ins to any dated activity). Self-scoped
/// via MyMemberResolver.
/// </summary>
public record GetMyStreaksQuery : IQuery<MyStreaksDto>;

public class GetMyStreaksQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetMyStreaksQuery, MyStreaksDto>
{
    public async Task<MyStreaksDto> Handle(GetMyStreaksQuery request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);
        // A streak is a count of weeks, so it is only as right as the day each activity is filed
        // under. In UTC a Sunday-evening session deserts the week it finished and lands in the next,
        // emptying a week the member actually trained and breaking the streak.
        var zone = await MyMemberResolver.ResolveGymZoneAsync(db, memberId, cancellationToken);
        var today = GymDay.Of(dateTimeProvider.UtcNow, zone);

        // Pull the raw timestamps (DateTimeOffset can't be aggregated/ordered in SQL on SQLite) and
        // reduce to dates + streaks in memory.
        var checkInDates = await db.AttendanceRecords.AsNoTracking()
            .Where(a => a.MemberId == memberId).Select(a => a.CheckInAt).ToListAsync(cancellationToken);

        var workoutDates = await db.WorkoutLogs.AsNoTracking()
            .Where(w => w.MemberId == memberId).Select(w => w.LoggedAt).ToListAsync(cancellationToken);

        var mealDates = await db.MealEntries.AsNoTracking()
            .Where(m => m.ConsumedAt != null && m.DietPlan!.MemberId == memberId)
            .Select(m => m.ConsumedAt!.Value)
            .ToListAsync(cancellationToken);

        return new MyStreaksDto(
            StreakCalculator.CurrentWeeklyStreak(checkInDates.Select(d => GymDay.Of(d, zone)), today),
            StreakCalculator.CurrentWeeklyStreak(workoutDates.Select(d => GymDay.Of(d, zone)), today),
            StreakCalculator.CurrentWeeklyStreak(mealDates.Select(d => GymDay.Of(d, zone)), today));
    }
}
