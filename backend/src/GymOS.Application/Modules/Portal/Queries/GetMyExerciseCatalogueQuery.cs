using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Portal.Dtos;
using GymOS.Domain.Workouts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Portal.Queries;

/// <summary>
/// Everything the member could train today, grouped the way they think about it, with what they last
/// did on each movement already attached.
///
/// This is the read behind "what am I doing in the gym today?". The design constraint the whole
/// screen turns on: the member should choose MOVEMENTS and type nothing else. Sets and weight are the
/// only two numbers anyone should have to enter, so everything a picker would otherwise ask for —
/// which muscle group, how many sets last time, what weight was on the bar — is answered here.
///
/// Three deliberate decisions:
///
/// - Grouping is by <see cref="MuscleGroupVocabulary"/>, not by the raw MuscleGroup string. Two gyms
///   typing "Legs" and "Lower body" get one Legs category, and nothing lands outside a category.
/// - <see cref="MyCatalogueExerciseDto.LastWeightKg"/> and LastReps come from the member's OWN
///   history and are null when there is none. An unfamiliar movement shows no numbers rather than a
///   default that looks like a recommendation.
/// - <see cref="MyCatalogueExerciseDto.LastPerformedAt"/> is what "Recent" is sorted by. Recency is
///   the single most useful ordering in a gym — the movement you did on Monday is the one you are
///   most likely to want on Thursday — and it costs nothing extra to compute here.
///
/// Self-scoped through MyMemberResolver like every other /api/me read. The catalogue itself is
/// tenant-scoped by the global query filter, so a member sees their own gym's movements and no
/// others'.
/// </summary>
public record GetMyExerciseCatalogueQuery : IQuery<MyExerciseCatalogueDto>;

public class GetMyExerciseCatalogueQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetMyExerciseCatalogueQuery, MyExerciseCatalogueDto>
{
    public async Task<MyExerciseCatalogueDto> Handle(
        GetMyExerciseCatalogueQuery request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);

        var exercises = await db.Exercises.AsNoTracking()
            .Select(e => new { e.Id, e.Name, e.MuscleGroup, e.Equipment, e.LoadType })
            .ToListAsync(cancellationToken);

        // The member's own history, reduced in memory: DateTimeOffset cannot be ordered in SQL on
        // SQLite, the same constraint every query in this module works around.
        var history = (await db.WorkoutLogEntries.AsNoTracking()
                .Where(e => e.WorkoutLog!.MemberId == memberId)
                .Select(e => new
                {
                    e.ExerciseId,
                    LoggedAt = e.WorkoutLog!.LoggedAt,
                    e.WorkoutLogId,
                    e.SetsCompleted,
                    e.RepsCompleted,
                    e.WeightKg
                })
                .ToListAsync(cancellationToken))
            .GroupBy(e => e.ExerciseId)
            .ToDictionary(g => g.Key, g =>
            {
                var lastLogId = g.OrderByDescending(e => e.LoggedAt).First().WorkoutLogId;

                // Everything from the SAME session, so "3 sets" means the three sets they actually
                // did that day — the logger writes one row per set, so a single row's SetsCompleted
                // would offer 1 to someone who did 4. Same reduction as the repeat-last proposal.
                var lastSession = g.Where(e => e.WorkoutLogId == lastLogId).ToList();
                var heaviest = lastSession
                    .OrderByDescending(e => e.WeightKg ?? 0)
                    .ThenByDescending(e => e.RepsCompleted)
                    .First();

                return new
                {
                    LastPerformedAt = g.Max(e => e.LoggedAt),
                    Sets = lastSession.Sum(e => e.SetsCompleted),
                    heaviest.RepsCompleted,
                    heaviest.WeightKg,
                    TimesPerformed = g.Select(e => e.WorkoutLogId).Distinct().Count()
                };
            });

        var groups = exercises
            .Select(e =>
            {
                var group = MuscleGroupVocabulary.Resolve(e.MuscleGroup);
                var last = history.GetValueOrDefault(e.Id);

                return new
                {
                    Group = group,
                    Dto = new MyCatalogueExerciseDto(
                        e.Id,
                        e.Name,
                        group.Key,
                        group.DisplayName,
                        e.Equipment,
                        e.LoadType.ToString(),
                        last?.LastPerformedAt,
                        last?.TimesPerformed ?? 0,
                        last?.Sets,
                        // Reps come back only where the movement has them. Serving the stored value
                        // unguarded is how the fabricated 8 was shown back to the member as their own
                        // history — "3 × 8 · 4d ago" for a treadmill — which made it indistinguishable
                        // from something they had really done.
                        last?.RepsCompleted,
                        // A load is only ever shown for a movement that has one. A plank's "weight"
                        // is not a small number, it is not a number.
                        e.LoadType == ExerciseLoadType.Weighted ? last?.WeightKg : null)
                };
            })
            .GroupBy(x => x.Group)
            .OrderBy(g => g.Key.Order)
            .Select(g => new MyExerciseGroupDto(
                g.Key.Key,
                g.Key.DisplayName,
                g.OrderBy(x => x.Dto.Name).Select(x => x.Dto).ToList()))
            .ToList();

        return new MyExerciseCatalogueDto(groups);
    }
}
