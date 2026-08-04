using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Workouts.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Workouts.Queries;

public record GetMemberWorkoutAssignmentsQuery(Guid MemberId) : IQuery<List<WorkoutAssignmentListItemDto>>;

public class GetMemberWorkoutAssignmentsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetMemberWorkoutAssignmentsQuery, List<WorkoutAssignmentListItemDto>>
{
    public async Task<List<WorkoutAssignmentListItemDto>> Handle(GetMemberWorkoutAssignmentsQuery request, CancellationToken cancellationToken)
    {
        var assignments = await db.WorkoutAssignments.AsNoTracking()
            .Where(a => a.MemberId == request.MemberId)
            .OrderByDescending(a => a.StartDate)
            .Select(a => new
            {
                a.Id,
                a.WorkoutTemplateId,
                TemplateName = a.WorkoutTemplate!.Name,
                TemplateDescription = a.WorkoutTemplate!.Description,
                a.StartDate,
                a.EndDate,
                a.Notes
            })
            .ToListAsync(cancellationToken);

        if (assignments.Count == 0)
        {
            return [];
        }

        var templateIds = assignments.Select(a => a.WorkoutTemplateId).Distinct().ToList();
        var exercisesByTemplate = (await db.WorkoutTemplateExercises.AsNoTracking()
                .Where(e => templateIds.Contains(e.WorkoutTemplateId))
                .OrderBy(e => e.OrderIndex)
                .Select(e => new { e.WorkoutTemplateId, e.Id, e.ExerciseId, ExerciseName = e.Exercise!.Name, e.SetsCount, e.RepsCount, e.OrderIndex })
                .ToListAsync(cancellationToken))
            .GroupBy(e => e.WorkoutTemplateId)
            .ToDictionary(g => g.Key, g => g.Select(e => new WorkoutTemplateExerciseDto(e.Id, e.ExerciseId, e.ExerciseName, e.SetsCount, e.RepsCount, e.OrderIndex)).ToList());

        return assignments
            .Select(a => new WorkoutAssignmentListItemDto(
                a.Id, a.WorkoutTemplateId, a.TemplateName, a.TemplateDescription, a.StartDate, a.EndDate, a.Notes,
                exercisesByTemplate.GetValueOrDefault(a.WorkoutTemplateId, [])))
            .ToList();
    }
}
