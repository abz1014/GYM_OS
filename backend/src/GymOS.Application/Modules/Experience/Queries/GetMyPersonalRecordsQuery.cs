using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Experience.Dtos;
using GymOS.Application.Modules.Portal;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Experience.Queries;

/// <summary>
/// The member's current personal records — the best value per (exercise, metric) from their
/// append-only PR ledger, with the exercise name resolved. Self-scoped via MyMemberResolver.
/// </summary>
public record GetMyPersonalRecordsQuery : IQuery<List<MyPersonalRecordDto>>;

public class GetMyPersonalRecordsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetMyPersonalRecordsQuery, List<MyPersonalRecordDto>>
{
    public async Task<List<MyPersonalRecordDto>> Handle(GetMyPersonalRecordsQuery request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);

        var records = await db.PersonalRecords.AsNoTracking()
            .Where(r => r.MemberId == memberId)
            .Select(r => new { r.ExerciseId, r.Type, r.Value, r.AchievedAt })
            .ToListAsync(cancellationToken);

        if (records.Count == 0)
        {
            return [];
        }

        var exerciseIds = records.Select(r => r.ExerciseId).Distinct().ToList();
        var names = await db.Exercises.AsNoTracking()
            .Where(e => exerciseIds.Contains(e.Id))
            .Select(e => new { e.Id, e.Name })
            .ToDictionaryAsync(e => e.Id, e => e.Name, cancellationToken);

        // Current record = the highest value per exercise+metric; keep the AchievedAt of that best
        // row. Ordering/aggregation done in memory (AchievedAt is a DateTimeOffset, which SQLite —
        // the test provider — can't order in SQL).
        return records
            .GroupBy(r => new { r.ExerciseId, r.Type })
            .Select(g =>
            {
                var best = g.OrderByDescending(x => x.Value).ThenByDescending(x => x.AchievedAt).First();
                return new MyPersonalRecordDto(
                    g.Key.ExerciseId,
                    names.GetValueOrDefault(g.Key.ExerciseId, "Unknown"),
                    g.Key.Type.ToString(),
                    best.Value,
                    best.AchievedAt);
            })
            .OrderBy(d => d.ExerciseName)
            .ThenBy(d => d.Type)
            .ToList();
    }
}
