using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Members.Commands;
using GymOS.Application.Modules.Nutrition.Commands;
using GymOS.Application.Modules.Workouts.Commands;
using GymOS.Domain.Nutrition;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Portal.Commands;

/// <summary>
/// The member's own data-entry surface. Until these existed the Member Experience Engine was
/// effectively inert for a real member: every XP award, personal record, mastery update, streak,
/// and challenge completion cascades off events that only a STAFF member could raise on their
/// behalf, so a member could see their progress but never actually create any.
///
/// Every command here follows the established portal pattern (see BookMyClassCommand): identity is
/// resolved from the JWT via MyMemberResolver and the work is then delegated to the existing staff
/// command, so validation and business rules live in exactly one place and the member path can
/// never diverge from the staff path. No command here accepts a member id from the caller.
/// </summary>
public record LogMyWorkoutCommand(IReadOnlyList<WorkoutLogEntryInput> Entries) : ICommand<Guid>;

public class LogMyWorkoutCommandValidator : AbstractValidator<LogMyWorkoutCommand>
{
    public LogMyWorkoutCommandValidator()
    {
        RuleFor(x => x.Entries).NotEmpty().WithMessage("Log at least one exercise.");
        RuleForEach(x => x.Entries).ChildRules(entry =>
        {
            entry.RuleFor(e => e.ExerciseId).NotEmpty();
            entry.RuleFor(e => e.SetsCompleted).GreaterThan(0).LessThanOrEqualTo(50);
            entry.RuleFor(e => e.RepsCompleted).GreaterThan(0).LessThanOrEqualTo(500);
            // A bodyweight movement legitimately has no weight, so null is allowed — but a supplied
            // weight must be sane. The upper bound is deliberately generous (well past any real lift)
            // and exists only to stop a fat-fingered entry poisoning mastery/PR projections.
            entry.RuleFor(e => e.WeightKg).InclusiveBetween(0m, 1000m).When(e => e.WeightKg.HasValue);
        });
    }
}

public class LogMyWorkoutCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, ISender sender)
    : IRequestHandler<LogMyWorkoutCommand, Guid>
{
    public async Task<Guid> Handle(LogMyWorkoutCommand request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);

        // Exercises are tenant-scoped, so this both validates the ids exist and prevents a member
        // referencing another tenant's exercise.
        var exerciseIds = request.Entries.Select(e => e.ExerciseId).Distinct().ToList();
        var knownCount = await db.Exercises.CountAsync(e => exerciseIds.Contains(e.Id), cancellationToken);
        if (knownCount != exerciseIds.Count)
        {
            throw new NotFoundException(nameof(GymOS.Domain.Workouts.Exercise), string.Join(", ", exerciseIds));
        }

        // Delegate: LogWorkoutCommand is what raises WorkoutLoggedEvent, which is what drives XP,
        // personal records, mastery, achievements, streaks and challenge progress.
        return await sender.Send(new LogWorkoutCommand(memberId, null, request.Entries), cancellationToken);
    }
}

/// <summary>A member logging their own water intake.</summary>
public record LogMyWaterCommand(int AmountMl) : ICommand<Guid>;

public class LogMyWaterCommandValidator : AbstractValidator<LogMyWaterCommand>
{
    public LogMyWaterCommandValidator()
        => RuleFor(x => x.AmountMl).GreaterThan(0).LessThanOrEqualTo(5000);
}

public class LogMyWaterCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, ISender sender)
    : IRequestHandler<LogMyWaterCommand, Guid>
{
    public async Task<Guid> Handle(LogMyWaterCommand request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);
        return await sender.Send(new LogWaterCommand(memberId, request.AmountMl), cancellationToken);
    }
}

/// <summary>
/// A member logging a meal against their own active diet plan. Deliberately does NOT take a
/// DietPlanId from the caller: the staff-facing AddMealEntryCommand does, which is correct for staff
/// (permission-gated) but would let one member write meal entries into another member's plan. The
/// plan is resolved server-side from the member's own active plans instead.
/// </summary>
public record LogMyMealCommand(Guid FoodItemId, MealType MealType, decimal Quantity) : ICommand<Guid>;

public class LogMyMealCommandValidator : AbstractValidator<LogMyMealCommand>
{
    public LogMyMealCommandValidator()
    {
        RuleFor(x => x.FoodItemId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0).LessThanOrEqualTo(100);
    }
}

public class LogMyMealCommandHandler(
    IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider, ISender sender)
    : IRequestHandler<LogMyMealCommand, Guid>
{
    public async Task<Guid> Handle(LogMyMealCommand request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);
        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);

        var planId = await db.DietPlans.AsNoTracking()
            .Where(p => p.MemberId == memberId && p.StartDate <= today && (p.EndDate == null || p.EndDate >= today))
            .OrderByDescending(p => p.StartDate)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (planId is null)
        {
            throw new NotFoundException("ActiveDietPlan", memberId);
        }

        return await sender.Send(new AddMealEntryCommand(planId.Value, request.FoodItemId, request.MealType, request.Quantity), cancellationToken);
    }
}

/// <summary>A member recording their own body measurements — the source series behind the weight
/// trend chart and the transformation timeline.</summary>
public record LogMyMeasurementCommand(
    decimal? WeightKg, decimal? BodyFatPercentage, decimal? ChestCm, decimal? WaistCm,
    decimal? HipCm, decimal? ArmCm, decimal? ThighCm, string? Notes) : ICommand<Guid>;

public class LogMyMeasurementCommandValidator : AbstractValidator<LogMyMeasurementCommand>
{
    public LogMyMeasurementCommandValidator()
    {
        RuleFor(x => x.WeightKg).InclusiveBetween(20m, 500m).When(x => x.WeightKg.HasValue);
        RuleFor(x => x.BodyFatPercentage).InclusiveBetween(1m, 70m).When(x => x.BodyFatPercentage.HasValue);
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x).Must(HasAtLeastOneValue)
            .WithMessage("Enter at least one measurement.");
    }

    private static bool HasAtLeastOneValue(LogMyMeasurementCommand c)
        => c.WeightKg.HasValue || c.BodyFatPercentage.HasValue || c.ChestCm.HasValue
           || c.WaistCm.HasValue || c.HipCm.HasValue || c.ArmCm.HasValue || c.ThighCm.HasValue;
}

public class LogMyMeasurementCommandHandler(
    IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider, ISender sender)
    : IRequestHandler<LogMyMeasurementCommand, Guid>
{
    public async Task<Guid> Handle(LogMyMeasurementCommand request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);
        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);

        return await sender.Send(
            new AddMeasurementCommand(
                memberId, today, request.WeightKg, request.BodyFatPercentage, request.ChestCm,
                request.WaistCm, request.HipCm, request.ArmCm, request.ThighCm, request.Notes),
            cancellationToken);
    }
}
