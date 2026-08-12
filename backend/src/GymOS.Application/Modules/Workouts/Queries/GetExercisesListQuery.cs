using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Workouts.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Workouts.Queries;

public record GetExercisesListQuery : IQuery<List<ExerciseDto>>;

public class GetExercisesListQueryHandler(IApplicationDbContext db) : IRequestHandler<GetExercisesListQuery, List<ExerciseDto>>
{
    public Task<List<ExerciseDto>> Handle(GetExercisesListQuery request, CancellationToken cancellationToken)
        => db.Exercises.AsNoTracking()
            .OrderBy(e => e.Name)
            .Select(e => new ExerciseDto(e.Id, e.Name, e.MuscleGroup, e.Equipment, e.Description, e.VideoUrl,
                e.LoadType.ToString()))
            .ToListAsync(cancellationToken);
}
