using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Workouts.Dtos;
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
        var exerciseNames = await db.Exercises.AsNoTracking()
            .Where(e => exerciseIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.Name, cancellationToken);

        return logs
            .Select(l => new WorkoutLogDto(
                l.Id, l.MemberId, l.WorkoutTemplateId,
                l.WorkoutTemplateId is not null && templateNames.TryGetValue(l.WorkoutTemplateId.Value, out var name) ? name : null,
                l.LoggedAt,
                l.Entries
                    .Select(e => new WorkoutLogEntryDto(
                        e.Id, e.ExerciseId, exerciseNames.GetValueOrDefault(e.ExerciseId, string.Empty), e.SetsCompleted, e.RepsCompleted, e.WeightKg))
                    .ToList()))
            .ToList();
    }
}
