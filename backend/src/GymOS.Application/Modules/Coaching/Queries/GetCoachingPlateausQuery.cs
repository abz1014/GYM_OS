using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Common.Security;
using GymOS.Application.Modules.Coaching.Dtos;
using GymOS.Domain.Members;
using GymOS.Domain.Workouts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Coaching.Queries;

/// <summary>
/// Every active member, across the caller's accessible branches, currently plateaued on an
/// exercise — the same ProgressiveOverloadPolicy signal GetMyWorkoutSuggestionsQuery already
/// computes per member, just run across a trainer's whole roster instead of "just me". A read-model
/// with no stored projection, same as the member-facing version.
/// </summary>
public record GetCoachingPlateausQuery : IQuery<List<PlateauRowDto>>;

public class GetCoachingPlateausQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetCoachingPlateausQuery, List<PlateauRowDto>>
{
    public async Task<List<PlateauRowDto>> Handle(GetCoachingPlateausQuery request, CancellationToken cancellationToken)
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
        var entries = await db.WorkoutLogEntries.AsNoTracking()
            .Where(e => memberIds.Contains(e.WorkoutLog!.MemberId))
            .Select(e => new { e.WorkoutLog!.MemberId, e.WorkoutLog!.LoggedAt, e.ExerciseId, e.WeightKg, e.SetsCompleted, e.RepsCompleted })
            .ToListAsync(cancellationToken);
        if (entries.Count == 0)
        {
            return [];
        }

        /*
         * WEIGHTED MOVEMENTS ONLY, and this was a live falsehood shown to staff.
         *
         * ProgressiveOverloadPolicy compares max weight and total reps across sessions and has no
         * other terms. GetMyWorkoutSuggestionsQuery — this query's own doc comment calls it "the same
         * signal … just run across a trainer's whole roster" — has filtered on LoadType since the
         * treadmill defect was found. This one never did. So the member screen correctly said nothing
         * about a run while the TRAINER screen listed that member as plateaued on it and invited a
         * message about adding weight.
         *
         * The trainer-facing version is the more expensive lie of the two, because a trainer acts on
         * it: they send the message.
         */
        var exerciseIds = entries.Select(e => e.ExerciseId).Distinct().ToList();
        var weightedExercises = await db.Exercises.AsNoTracking()
            .Where(x => exerciseIds.Contains(x.Id) && x.LoadType == ExerciseLoadType.Weighted)
            .Select(x => new { x.Id, x.Name })
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        var exerciseNames = weightedExercises;
        entries = entries.Where(e => weightedExercises.ContainsKey(e.ExerciseId)).ToList();
        if (entries.Count == 0)
        {
            return [];
        }
        var memberById = members.ToDictionary(m => m.Id);

        var rows = new List<PlateauRowDto>();
        foreach (var byMember in entries.GroupBy(e => e.MemberId))
        {
            foreach (var byExercise in byMember.GroupBy(e => e.ExerciseId))
            {
                // One row per (session, exercise): mirrors GetMyWorkoutSuggestionsQuery's own reduction.
                var sessionsOldestFirst = byExercise
                    .GroupBy(e => e.LoggedAt)
                    .Select(g => new
                    {
                        LoggedAt = g.Key,
                        MaxWeightKg = g.Max(x => x.WeightKg) ?? 0m,
                        TotalReps = g.Sum(x => x.SetsCompleted * (x.RepsCompleted ?? 0))
                    })
                    .OrderBy(s => s.LoggedAt)
                    .ToList();

                var performances = sessionsOldestFirst.Select(s => new ExercisePerformance(s.MaxWeightKg, s.TotalReps)).ToList();
                if (ProgressiveOverloadPolicy.Evaluate(performances) != OverloadSuggestion.ReadyToIncreaseWeight)
                {
                    continue;
                }

                if (!exerciseNames.TryGetValue(byExercise.Key, out var exerciseName))
                {
                    continue; // exercise deleted since it was logged
                }

                var last = sessionsOldestFirst[^1];
                var member = memberById[byMember.Key];
                rows.Add(new PlateauRowDto(
                    member.Id, $"{member.FirstName} {member.LastName}", member.MemberCode,
                    byExercise.Key, exerciseName,
                    last.MaxWeightKg > 0 ? last.MaxWeightKg : null,
                    last.MaxWeightKg > 0 ? ProgressiveOverloadPolicy.SuggestedNextWeightKg(last.MaxWeightKg) : null,
                    last.LoggedAt));
            }
        }

        return rows.OrderByDescending(r => r.LastLoggedAt).ToList();
    }
}
