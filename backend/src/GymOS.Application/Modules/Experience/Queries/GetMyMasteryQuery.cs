using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Experience.Dtos;
using GymOS.Application.Modules.Portal;
using GymOS.Domain.Experience;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Experience.Queries;

/// <summary>
/// The member's mastery in one round trip: per exercise, plus muscle-group and machine (equipment)
/// breakdowns aggregated from the same ExerciseMastery projections (no separate tables). Mastery % is
/// computed here via the pure MasteryPolicy since it isn't stored. Self-scoped via MyMemberResolver.
/// </summary>
public record GetMyMasteryQuery : IQuery<MyMasteryDto>;

public class GetMyMasteryQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetMyMasteryQuery, MyMasteryDto>
{
    private sealed record Row(string ExerciseName, string? MuscleGroup, string? Equipment, int Sessions, decimal TotalVolume,
        decimal BestWeightKg, decimal BestOneRm, Guid ExerciseId, DateTimeOffset LastTrainedAt);

    public async Task<MyMasteryDto> Handle(GetMyMasteryQuery request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);

        var masteries = await db.ExerciseMasteries.AsNoTracking()
            .Where(m => m.MemberId == memberId)
            .Select(m => new { m.ExerciseId, m.Sessions, m.TotalVolume, m.BestWeightKg, m.BestEstimatedOneRepMax, m.LastTrainedAt })
            .ToListAsync(cancellationToken);

        if (masteries.Count == 0)
        {
            return new MyMasteryDto([], [], []);
        }

        var exerciseIds = masteries.Select(m => m.ExerciseId).Distinct().ToList();
        var exMap = await db.Exercises.AsNoTracking()
            .Where(e => exerciseIds.Contains(e.Id))
            .Select(e => new { e.Id, e.Name, e.MuscleGroup, e.Equipment })
            .ToDictionaryAsync(e => e.Id, cancellationToken);

        // Flatten to a single named shape so the per-exercise DTOs and both group breakdowns all read
        // from the same in-memory rows.
        var rows = masteries.Select(m =>
        {
            exMap.TryGetValue(m.ExerciseId, out var ex);
            return new Row(ex?.Name ?? "Unknown", ex?.MuscleGroup, ex?.Equipment, m.Sessions, m.TotalVolume,
                m.BestWeightKg, m.BestEstimatedOneRepMax, m.ExerciseId, m.LastTrainedAt);
        }).ToList();

        var exerciseDtos = rows
            .Select(r => new ExerciseMasteryDto(
                r.ExerciseId, r.ExerciseName, r.MuscleGroup, r.Equipment, r.Sessions, r.BestWeightKg, r.BestOneRm,
                r.TotalVolume, MasteryPolicy.MasteryPercent(r.Sessions, r.TotalVolume), r.LastTrainedAt))
            .OrderByDescending(d => d.MasteryPercent)
            .ThenBy(d => d.ExerciseName)
            .ToList();

        return new MyMasteryDto(exerciseDtos, GroupBreakdown(rows, r => r.MuscleGroup), GroupBreakdown(rows, r => r.Equipment));
    }

    // Aggregates per-exercise rows into a named group (muscle group or machine). Sessions are summed
    // across the group's exercises — an aggregate "how much you've trained this" signal, not a
    // distinct-session count — which the bounded MasteryPolicy turns into a percent.
    private static List<GroupMasteryDto> GroupBreakdown(IEnumerable<Row> rows, Func<Row, string?> keySelector)
        => rows
            .Where(r => !string.IsNullOrWhiteSpace(keySelector(r)))
            .GroupBy(keySelector)
            .Select(g =>
            {
                var sessions = g.Sum(r => r.Sessions);
                var volume = g.Sum(r => r.TotalVolume);
                return new GroupMasteryDto(g.Key!, sessions, volume, MasteryPolicy.MasteryPercent(sessions, volume));
            })
            .OrderByDescending(d => d.MasteryPercent)
            .ThenBy(d => d.Name)
            .ToList();
}
