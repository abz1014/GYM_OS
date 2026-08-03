using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Workouts.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Workouts.Queries;

public record GetWorkoutTemplatesListQuery : IQuery<List<WorkoutTemplateListItemDto>>;

public class GetWorkoutTemplatesListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetWorkoutTemplatesListQuery, List<WorkoutTemplateListItemDto>>
{
    public Task<List<WorkoutTemplateListItemDto>> Handle(GetWorkoutTemplatesListQuery request, CancellationToken cancellationToken)
        => db.WorkoutTemplates.AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new WorkoutTemplateListItemDto(t.Id, t.Name, t.Description, t.TemplateExercises.Count))
            .ToListAsync(cancellationToken);
}
