using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Workouts;
using MediatR;

namespace GymOS.Application.Modules.Workouts.Commands;

public record WorkoutTemplateExerciseInput(Guid ExerciseId, int SetsCount, int RepsCount, int OrderIndex);

public record CreateWorkoutTemplateCommand(string Name, string? Description, IReadOnlyList<WorkoutTemplateExerciseInput> Exercises)
    : ICommand<Guid>;

public class CreateWorkoutTemplateCommandValidator : AbstractValidator<CreateWorkoutTemplateCommand>
{
    public CreateWorkoutTemplateCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Exercises).NotEmpty();
    }
}

public class CreateWorkoutTemplateCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<CreateWorkoutTemplateCommand, Guid>
{
    public async Task<Guid> Handle(CreateWorkoutTemplateCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new ForbiddenAccessException("No tenant context.");

        var template = new WorkoutTemplate
        {
            TenantId = tenantId,
            Name = request.Name,
            Description = request.Description,
            CreatedByUserId = currentUser.UserId
        };

        foreach (var exercise in request.Exercises)
        {
            template.TemplateExercises.Add(new WorkoutTemplateExercise
            {
                ExerciseId = exercise.ExerciseId,
                SetsCount = exercise.SetsCount,
                RepsCount = exercise.RepsCount,
                OrderIndex = exercise.OrderIndex
            });
        }

        db.WorkoutTemplates.Add(template);
        await db.SaveChangesAsync(cancellationToken);

        return template.Id;
    }
}
