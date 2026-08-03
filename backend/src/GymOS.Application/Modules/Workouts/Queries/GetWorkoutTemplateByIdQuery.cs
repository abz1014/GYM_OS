using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Workouts.Dtos;
using GymOS.Domain.Workouts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Workouts.Queries;

public record GetWorkoutTemplateByIdQuery(Guid Id) : IQuery<WorkoutTemplateDetailDto>;

public class GetWorkoutTemplateByIdQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetWorkoutTemplateByIdQuery, WorkoutTemplateDetailDto>
{
    public async Task<WorkoutTemplateDetailDto> Handle(GetWorkoutTemplateByIdQuery request, CancellationToken cancellationToken)
    {
        var template = await db.WorkoutTemplates.AsNoTracking()
            .Include(t => t.TemplateExercises).ThenInclude(te => te.Exercise)
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(WorkoutTemplate), request.Id);

        return new WorkoutTemplateDetailDto(
            template.Id, template.Name, template.Description,
            template.TemplateExercises
                .OrderBy(te => te.OrderIndex)
                .Select(te => new WorkoutTemplateExerciseDto(te.Id, te.ExerciseId, te.Exercise?.Name ?? string.Empty, te.SetsCount, te.RepsCount, te.OrderIndex))
                .ToList());
    }
}
