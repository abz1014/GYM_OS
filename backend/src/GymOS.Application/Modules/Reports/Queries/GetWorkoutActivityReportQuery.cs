using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Reports.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Reports.Queries;

public record GetWorkoutActivityReportQuery(int DaysBack = 30) : IQuery<List<WorkoutActivityReportRowDto>>;

public class GetWorkoutActivityReportQueryHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetWorkoutActivityReportQuery, List<WorkoutActivityReportRowDto>>
{
    public async Task<List<WorkoutActivityReportRowDto>> Handle(GetWorkoutActivityReportQuery request, CancellationToken cancellationToken)
        => await BuildAsync(db, dateTimeProvider, request.DaysBack, cancellationToken);

    internal static async Task<List<WorkoutActivityReportRowDto>> BuildAsync(
        IApplicationDbContext db, IDateTimeProvider dateTimeProvider, int daysBack, CancellationToken cancellationToken)
    {
        var cutoff = dateTimeProvider.UtcNow.AddDays(-daysBack);

        // WorkoutLog/WorkoutLogEntry aren't tenant-scoped themselves — joining through Exercise
        // (which is) naturally restricts results to the current tenant, the same pattern
        // CommissionRecord/Trainers relies on in GetTrainerCommissionReportQuery.
        var entries = await db.WorkoutLogEntries.AsNoTracking()
            .Join(db.WorkoutLogs, e => e.WorkoutLogId, l => l.Id, (e, l) => new { e, l.LoggedAt })
            .Where(x => x.LoggedAt >= cutoff)
            .Join(db.Exercises, x => x.e.ExerciseId, ex => ex.Id, (x, ex) => new
            {
                ex.Name,
                ex.MuscleGroup,
                x.e.SetsCompleted,
                x.e.RepsCompleted,
                x.e.WeightKg,
                x.e.DurationSeconds,
                x.e.DistanceMeters
            })
            .ToListAsync(cancellationToken);

        return entries
            .GroupBy(e => new { e.Name, e.MuscleGroup })
            .Select(g => new WorkoutActivityReportRowDto(
                g.Key.Name,
                g.Key.MuscleGroup,
                g.Count(),
                g.Sum(x => x.SetsCompleted),
                // Each of these is null unless at least one row actually carried the measurement, so
                // a treadmill reports distance and duration and stays blank on reps, rather than
                // reporting a confident zero for all three.
                g.Any(x => x.RepsCompleted is not null) ? g.Sum(x => x.RepsCompleted ?? 0) : null,
                g.Any(x => x.WeightKg is not null) ? Math.Round(g.Where(x => x.WeightKg is not null).Average(x => x.WeightKg!.Value), 1) : null,
                g.Any(x => x.DurationSeconds is not null) ? g.Sum(x => x.DurationSeconds ?? 0) : null,
                g.Any(x => x.DistanceMeters is not null) ? g.Sum(x => x.DistanceMeters ?? 0m) : null))
            .OrderByDescending(r => r.TimesLogged)
            .ToList();
    }
}
