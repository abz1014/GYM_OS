using GymOS.Application.Common.Interfaces;
using GymOS.Domain.Common;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Portal.Dtos;
using GymOS.Domain.Members;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Portal.Queries;

/// <summary>
/// Everything the member's progress page shows in one round trip: their weekly streak (computed by
/// the pure StreakCalculator from their own check-ins), visit counts, weight trend from logged
/// measurements, their goals, and their progress photos. All of it is data the gym already
/// collects — this query's job is to turn it back TOWARD the member as motivation, instead of it
/// only being a staff-side record. Identity via MyMemberResolver, as everywhere in /api/me.
/// </summary>
public record GetMyProgressQuery : IQuery<MyProgressDto>;

public class GetMyProgressQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetMyProgressQuery, MyProgressDto>
{
    public async Task<MyProgressDto> Handle(GetMyProgressQuery request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);
        var zone = await MyMemberResolver.ResolveGymZoneAsync(db, memberId, cancellationToken);
        var today = GymDay.Of(dateTimeProvider.UtcNow, zone);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var checkInDates = await db.AttendanceRecords.AsNoTracking()
            .Where(a => a.MemberId == memberId)
            .Select(a => a.CheckInAt)
            .ToListAsync(cancellationToken);

        var checkInDays = checkInDates.Select(c => GymDay.Of(c, zone)).ToList();

        var measurements = await db.MemberMeasurements.AsNoTracking()
            .Where(m => m.MemberId == memberId && m.WeightKg != null)
            .OrderBy(m => m.MeasuredOn)
            .Select(m => new MyWeightPointDto(m.MeasuredOn, m.WeightKg!.Value))
            .ToListAsync(cancellationToken);

        var goals = await db.MemberGoals.AsNoTracking()
            .Where(g => g.MemberId == memberId)
            .OrderBy(g => g.IsAchieved).ThenBy(g => g.TargetDate)
            .Select(g => new MyGoalDto(g.Id, g.Title, g.TargetDate, g.IsAchieved, g.AchievedAt))
            .ToListAsync(cancellationToken);

        var photos = await db.ProgressPhotos.AsNoTracking()
            .Where(p => p.MemberId == memberId)
            .OrderBy(p => p.TakenAt)
            .Select(p => new MyProgressPhotoDto(p.Id, p.PhotoUrl, p.TakenAt, p.Notes))
            .ToListAsync(cancellationToken);

        return new MyProgressDto(
            StreakCalculator.CurrentWeeklyStreak(checkInDays, today),
            checkInDays.Count,
            checkInDays.Count(d => d >= monthStart),
            measurements,
            goals,
            photos);
    }
}
