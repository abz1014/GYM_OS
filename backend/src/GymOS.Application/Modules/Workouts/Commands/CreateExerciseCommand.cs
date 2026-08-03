using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Workouts;
using MediatR;

namespace GymOS.Application.Modules.Workouts.Commands;

public record CreateExerciseCommand(string Name, string? MuscleGroup, string? Equipment, string? Description, string? VideoUrl)
    : ICommand<Guid>;

public class CreateExerciseCommandValidator : AbstractValidator<CreateExerciseCommand>
{
    public CreateExerciseCommandValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
}

public class CreateExerciseCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<CreateExerciseCommand, Guid>
{
    public async Task<Guid> Handle(CreateExerciseCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new ForbiddenAccessException("No tenant context.");

        var exercise = new Exercise
        {
            TenantId = tenantId,
            Name = request.Name,
            MuscleGroup = request.MuscleGroup,
            Equipment = request.Equipment,
            Description = request.Description,
            VideoUrl = request.VideoUrl
        };

        db.Exercises.Add(exercise);
        await db.SaveChangesAsync(cancellationToken);

        return exercise.Id;
    }
}
