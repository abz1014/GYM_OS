using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Domain.Workouts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Workouts.Commands;

/// <summary>
/// One movement in a logged session. Which of these fields carry a value is decided by the
/// exercise's <see cref="ExerciseLoadType"/> — see <see cref="LogWorkoutCommandValidator"/>.
/// </summary>
/// <param name="RepsCompleted">Null for a movement with no reps. Optional-with-a-default so the
/// dozens of existing call sites that pass four positional arguments keep compiling.</param>
public record WorkoutLogEntryInput(
    Guid ExerciseId,
    int SetsCompleted,
    int? RepsCompleted,
    decimal? WeightKg,
    int? DurationSeconds = null,
    decimal? DistanceMeters = null);

public record LogWorkoutCommand(Guid MemberId, Guid? WorkoutTemplateId, IReadOnlyList<WorkoutLogEntryInput> Entries) : ICommand<Guid>;

/// <summary>
/// The shape rules, on the SHARED command rather than only the member-facing one.
///
/// This validator previously contained nothing but "a member id and at least one entry", while the
/// portal wrapper carried the real rules — so the staff dialog, which writes through this same
/// handler, was the weaker door into the same table. Rules belong where the write happens.
///
/// The per-field bounds are deliberately generous and their job is to catch nonsense, not to coach:
/// 500 reps, 1000kg, 12 hours, 100km. The load-type rules that actually prevent a fabricated number
/// need the exercise rows, so they live in the handler where those are already loaded.
/// </summary>
public class LogWorkoutCommandValidator : AbstractValidator<LogWorkoutCommand>
{
    public LogWorkoutCommandValidator()
    {
        RuleFor(x => x.MemberId).NotEmpty();
        RuleFor(x => x.Entries).NotEmpty();

        RuleForEach(x => x.Entries).ChildRules(entry =>
        {
            entry.RuleFor(e => e.ExerciseId).NotEmpty();
            entry.RuleFor(e => e.SetsCompleted).GreaterThan(0).LessThanOrEqualTo(50);
            entry.RuleFor(e => e.RepsCompleted).GreaterThan(0).LessThanOrEqualTo(500)
                .When(e => e.RepsCompleted.HasValue);
            entry.RuleFor(e => e.WeightKg).GreaterThan(0).LessThanOrEqualTo(1000)
                .When(e => e.WeightKg.HasValue);
            entry.RuleFor(e => e.DurationSeconds).GreaterThan(0).LessThanOrEqualTo(43_200)
                .When(e => e.DurationSeconds.HasValue);
            entry.RuleFor(e => e.DistanceMeters).GreaterThan(0).LessThanOrEqualTo(100_000)
                .When(e => e.DistanceMeters.HasValue);
        });
    }
}

public class LogWorkoutCommandHandler(IApplicationDbContext db, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<LogWorkoutCommand, Guid>
{
    public async Task<Guid> Handle(LogWorkoutCommand request, CancellationToken cancellationToken)
    {
        var memberExists = await db.Members.AnyAsync(m => m.Id == request.MemberId, cancellationToken);
        if (!memberExists)
        {
            throw new NotFoundException(nameof(Domain.Members.Member), request.MemberId);
        }

        /*
         * A movement may only be measured the way it is actually measured.
         *
         * This is the guard that makes the fabricated rep count impossible rather than merely
         * discouraged. Nothing between the member's tap and the INSERT used to know a treadmill from
         * a bench press: the picker applied a DEFAULT_REPS of 8 to everything, the API demanded a
         * rep count, and "8 reps of running" became a stored fact that the next-session proposal
         * then re-served forever.
         *
         * Rejecting rather than silently dropping. A caller sending reps for a run has a bug, and
         * quietly nulling the field would leave that bug in place and unfindable — the same reasoning
         * as the payment ceiling, where accepting-and-clamping was rejected in favour of refusing.
         */
        var exerciseIds = request.Entries.Select(e => e.ExerciseId).Distinct().ToList();
        var loadTypes = await db.Exercises.AsNoTracking()
            .Where(x => exerciseIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Name, x.LoadType })
            .ToDictionaryAsync(x => x.Id, x => x, cancellationToken);

        foreach (var entry in request.Entries)
        {
            if (!loadTypes.TryGetValue(entry.ExerciseId, out var exercise))
            {
                throw new NotFoundException(nameof(Exercise), entry.ExerciseId);
            }

            var repsAllowed = exercise.LoadType is ExerciseLoadType.Weighted or ExerciseLoadType.Bodyweight;
            if (!repsAllowed && entry.RepsCompleted.HasValue)
            {
                throw new ValidationException(
                    $"{exercise.Name} is measured in {(exercise.LoadType == ExerciseLoadType.Timed ? "time" : "distance")}, not repetitions.");
            }

            if (exercise.LoadType == ExerciseLoadType.Timed && entry.DistanceMeters.HasValue)
            {
                throw new ValidationException($"{exercise.Name} is measured in time, not distance.");
            }

            // Weight is NOT forbidden on a Distance movement: a farmer's carry and a weighted-vest
            // run are both real, and the seeded catalogue contains the former. LoadType names the
            // primary measurement, not the only permissible one.
            if (exercise.LoadType is ExerciseLoadType.Bodyweight or ExerciseLoadType.Timed && entry.WeightKg.HasValue)
            {
                throw new ValidationException($"{exercise.Name} carries no external load.");
            }
        }

        var log = new WorkoutLog
        {
            MemberId = request.MemberId,
            WorkoutTemplateId = request.WorkoutTemplateId,
            LoggedAt = dateTimeProvider.UtcNow
        };

        foreach (var entry in request.Entries)
        {
            log.Entries.Add(new WorkoutLogEntry
            {
                ExerciseId = entry.ExerciseId,
                SetsCompleted = entry.SetsCompleted,
                RepsCompleted = entry.RepsCompleted,
                WeightKg = entry.WeightKg,
                DurationSeconds = entry.DurationSeconds,
                DistanceMeters = entry.DistanceMeters
            });
        }

        log.RaiseLogged();

        db.WorkoutLogs.Add(log);
        await db.SaveChangesAsync(cancellationToken);

        return log.Id;
    }
}
