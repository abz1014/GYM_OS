using GymOS.Application.Common.Interfaces;
using GymOS.Domain.Common;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Portal.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Portal.Queries;

/// <summary>
/// Everything the member's logging screen needs to populate its pickers in one round trip: the
/// tenant's exercise catalogue (so they can pick "Deadlift"), the food catalogue, and whether they
/// currently have an active diet plan (meal logging is only meaningful against one). Read-only and
/// self-scoped; the catalogues are already tenant-filtered by the global query filter.
/// </summary>
public record GetMyLoggingOptionsQuery : IQuery<MyLoggingOptionsDto>;

public class GetMyLoggingOptionsQueryHandler(
    IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetMyLoggingOptionsQuery, MyLoggingOptionsDto>
{
    public async Task<MyLoggingOptionsDto> Handle(GetMyLoggingOptionsQuery request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);
        var zone = await MyMemberResolver.ResolveGymZoneAsync(db, memberId, cancellationToken);
        var today = GymDay.Of(dateTimeProvider.UtcNow, zone);

        var exercises = await db.Exercises.AsNoTracking()
            .OrderBy(e => e.Name)
            .Select(e => new LoggableExerciseDto(e.Id, e.Name, e.MuscleGroup, e.Equipment, e.LoadType.ToString()))
            .ToListAsync(cancellationToken);

        var foods = await db.FoodItems.AsNoTracking()
            .OrderBy(f => f.Name)
            .Select(f => new LoggableFoodDto(f.Id, f.Name, f.CaloriesPerServing, f.ProteinG, f.ServingSizeDescription))
            .ToListAsync(cancellationToken);

        var activePlanName = await db.DietPlans.AsNoTracking()
            .Where(p => p.MemberId == memberId && p.StartDate <= today && (p.EndDate == null || p.EndDate >= today))
            .OrderByDescending(p => p.StartDate)
            .Select(p => p.Name)
            .FirstOrDefaultAsync(cancellationToken);

        return new MyLoggingOptionsDto(exercises, foods, activePlanName);
    }
}

/// <summary>
/// The member's own body-measurement history, oldest first — the series behind the weight and
/// body-fat trend charts. Kept separate from GetMyProgressQuery (which returns only a summary) so
/// the chart can plot every point without the summary query growing an unbounded payload.
/// </summary>
public record GetMyMeasurementsQuery : IQuery<List<MyMeasurementDto>>;

public class GetMyMeasurementsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetMyMeasurementsQuery, List<MyMeasurementDto>>
{
    public async Task<List<MyMeasurementDto>> Handle(GetMyMeasurementsQuery request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);
        var zone = await MyMemberResolver.ResolveGymZoneAsync(db, memberId, cancellationToken);

        var rows = await db.MemberMeasurements.AsNoTracking()
            .Where(m => m.MemberId == memberId)
            .Select(m => new MyMeasurementDto(
                m.Id, m.MeasuredOn, m.WeightKg, m.BodyFatPercentage,
                m.ChestCm, m.WaistCm, m.HipCm, m.ArmCm, m.ThighCm, m.Notes))
            .ToListAsync(cancellationToken);

        return rows.OrderBy(m => m.MeasuredOn).ToList();
    }
}

/// <summary>
/// Per-day training volume for the member's trailing window — the series behind the "training
/// volume" trend chart, which previously had no endpoint at all (the portal could show today's
/// numbers but never a shape over time). Volume is sets x reps x weight, the same reduction
/// ExerciseMastery accumulates.
/// </summary>
public record GetMyTrainingVolumeQuery(int DaysBack = 30) : IQuery<List<MyDailyVolumeDto>>;

public class GetMyTrainingVolumeQueryHandler(
    IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetMyTrainingVolumeQuery, List<MyDailyVolumeDto>>
{
    public async Task<List<MyDailyVolumeDto>> Handle(GetMyTrainingVolumeQuery request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);
        var zone = await MyMemberResolver.ResolveGymZoneAsync(db, memberId, cancellationToken);
        var daysBack = Math.Clamp(request.DaysBack, 7, 365);
        var today = GymDay.Of(dateTimeProvider.UtcNow, zone);
        var windowStart = today.AddDays(-(daysBack - 1));

        // DateTimeOffset can't be bucketed to a date in SQL on SQLite, so reduce in memory — the
        // same pattern every other MEE query over WorkoutLogs uses.
        var rows = await db.WorkoutLogEntries.AsNoTracking()
            .Where(e => e.WorkoutLog!.MemberId == memberId)
            .Select(e => new { e.WorkoutLog!.LoggedAt, e.SetsCompleted, e.RepsCompleted, e.WeightKg })
            .ToListAsync(cancellationToken);

        var byDay = rows
            .Select(r => new
            {
                Date = GymDay.Of(r.LoggedAt, zone),
                Volume = r.SetsCompleted * (r.RepsCompleted ?? 0) * (r.WeightKg ?? 0m),
                Reps = r.SetsCompleted * (r.RepsCompleted ?? 0)
            })
            .Where(r => r.Date >= windowStart && r.Date <= today)
            .GroupBy(r => r.Date)
            .ToDictionary(g => g.Key, g => (Volume: g.Sum(x => x.Volume), Reps: g.Sum(x => x.Reps)));

        // Emit a continuous series including rest days as zero, so the chart shows real cadence
        // rather than silently compressing gaps between sessions.
        var series = new List<MyDailyVolumeDto>();
        for (var d = windowStart; d <= today; d = d.AddDays(1))
        {
            byDay.TryGetValue(d, out var totals);
            series.Add(new MyDailyVolumeDto(d, totals.Volume, totals.Reps));
        }

        return series;
    }
}
