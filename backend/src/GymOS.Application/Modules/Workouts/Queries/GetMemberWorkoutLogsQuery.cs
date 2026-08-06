using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Workouts.Dtos;
using GymOS.Domain.Workouts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Workouts.Queries;

public record GetMemberWorkoutLogsQuery(Guid MemberId) : IQuery<List<WorkoutLogDto>>;

public class GetMemberWorkoutLogsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetMemberWorkoutLogsQuery, List<WorkoutLogDto>>
{
    public async Task<List<WorkoutLogDto>> Handle(GetMemberWorkoutLogsQuery request, CancellationToken cancellationToken)
    {
        var logs = await db.WorkoutLogs.AsNoTracking()
            .Include(l => l.Entries)
            .Where(l => l.MemberId == request.MemberId)
            .OrderByDescending(l => l.LoggedAt)
            .ToListAsync(cancellationToken);

        var templateIds = logs.Where(l => l.WorkoutTemplateId is not null).Select(l => l.WorkoutTemplateId!.Value).Distinct().ToList();
        var templateNames = await db.WorkoutTemplates.AsNoTracking()
            .Where(t => templateIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);

        var exerciseIds = logs.SelectMany(l => l.Entries.Select(e => e.ExerciseId)).Distinct().ToList();
        // Muscle group rides along with the name because it is what gives the session its character —
        // otherwise every self-logged workout is presented back to the member as "Custom workout".
        var exercises = await db.Exercises.AsNoTracking()
            .Where(e => exerciseIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => new { e.Name, e.MuscleGroup }, cancellationToken);

        return logs
            .Select(l => new WorkoutLogDto(
                l.Id, l.MemberId, l.WorkoutTemplateId,
                l.WorkoutTemplateId is not null && templateNames.TryGetValue(l.WorkoutTemplateId.Value, out var name) ? name : null,
                SessionCharacterPolicy.Describe(l.Entries.Select(e => exercises.GetValueOrDefault(e.ExerciseId)?.MuscleGroup)),
                l.LoggedAt,
                l.Entries
                    .Select(e => new WorkoutLogEntryDto(
                        e.Id, e.ExerciseId, exercises.GetValueOrDefault(e.ExerciseId)?.Name ?? string.Empty, e.SetsCompleted, e.RepsCompleted, e.WeightKg))
                    .ToList()))
            .ToList();
    }
}
