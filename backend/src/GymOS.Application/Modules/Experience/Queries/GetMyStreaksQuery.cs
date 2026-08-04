using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Experience.Dtos;
using GymOS.Application.Modules.Portal;
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
        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);

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
            StreakCalculator.CurrentWeeklyStreak(checkInDates.Select(ToDate), today),
            StreakCalculator.CurrentWeeklyStreak(workoutDates.Select(ToDate), today),
            StreakCalculator.CurrentWeeklyStreak(mealDates.Select(ToDate), today));
    }

    private static DateOnly ToDate(DateTimeOffset value) => DateOnly.FromDateTime(value.UtcDateTime);
}
