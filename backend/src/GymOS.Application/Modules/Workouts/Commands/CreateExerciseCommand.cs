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

        /*
         * The primary muscle row, resolved from the label the gym just typed.
         *
         * Written here rather than left for a backfill because the recovery map reads ExerciseMuscles
         * — a movement created without one would fall back to its label and behave as it did before,
         * which is survivable but means a gym's own movements quietly get worse treatment than the
         * ones we shipped.
         *
         * NO SECONDARIES ARE GUESSED. We know what a deadlift works because someone wrote it down;
         * we do not know what this gym's "Sled Push" works, and inferring it from a name would be the
         * app inventing anatomy. A gym that wants secondary groups on its own movements will need a
         * way to say so — that editor does not exist yet, and pretending otherwise by guessing would
         * be worse than the gap.
         */
        exercise.Muscles.Add(new ExerciseMuscle
        {
            TenantId = tenantId,
            MuscleGroupKey = MuscleGroupVocabulary.Resolve(request.MuscleGroup).Key,
            Role = MuscleRole.Primary
        });

        db.Exercises.Add(exercise);
        await db.SaveChangesAsync(cancellationToken);

        return exercise.Id;
    }
}
