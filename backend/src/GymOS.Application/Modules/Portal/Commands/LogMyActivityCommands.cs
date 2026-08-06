using FluentValidation;
using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Domain.Common;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Challenges.Queries;
using GymOS.Application.Modules.Members.Commands;
using GymOS.Application.Modules.Nutrition.Commands;
using GymOS.Application.Modules.Portal.Dtos;
using GymOS.Application.Modules.Workouts.Commands;
using GymOS.Domain.Experience;
using GymOS.Domain.Members;
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
public record LogMyWorkoutCommand(IReadOnlyList<WorkoutLogEntryInput> Entries) : ICommand<MyWorkoutResultDto>;

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

public class LogMyWorkoutCommandHandler(
    IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider, ISender sender)
    : IRequestHandler<LogMyWorkoutCommand, MyWorkoutResultDto>
{
    public async Task<MyWorkoutResultDto> Handle(LogMyWorkoutCommand request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);
        var zone = await MyMemberResolver.ResolveGymZoneAsync(db, memberId, cancellationToken);
        var today = GymDay.Of(dateTimeProvider.UtcNow, zone);

        // Exercises are tenant-scoped, so this both validates the ids exist and prevents a member
        // referencing another tenant's exercise.
        var exerciseIds = request.Entries.Select(e => e.ExerciseId).Distinct().ToList();
        // Muscle groups come back from the same lookup that validates the ids — they are what names
        // the session, so the member is told what they just did rather than "workout logged".
        var muscleGroupById = await db.Exercises.AsNoTracking()
            .Where(e => exerciseIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.MuscleGroup, cancellationToken);
        if (muscleGroupById.Count != exerciseIds.Count)
        {
            throw new NotFoundException(nameof(GymOS.Domain.Workouts.Exercise), string.Join(", ", exerciseIds));
        }

        var character = GymOS.Domain.Workouts.SessionCharacterPolicy.Describe(
            request.Entries.Select(e => muscleGroupById.GetValueOrDefault(e.ExerciseId)));

        // Snapshot the "before" side of everything worth celebrating. Reported figures are a genuine
        // diff across the log rather than the client's guess at what the engine would award.
        var (xpBefore, levelBefore) = await ReadProgressionAsync(memberId, cancellationToken);
        var achievementsBefore = await db.MemberAchievements.AsNoTracking()
            .Where(a => a.MemberId == memberId).Select(a => a.Code).ToListAsync(cancellationToken);
        var sessionsBefore = WeeklyGoalPolicy.SessionsThisWeek(await WorkoutDatesAsync(memberId, zone, cancellationToken), today);
        var challengesBefore = (await sender.Send(new GetMyChallengesQuery(), cancellationToken))
            .ToDictionary(c => c.Id, c => c.IsCompleted);

        // Delegate: LogWorkoutCommand is what raises WorkoutLoggedEvent, which is what drives XP,
        // personal records, mastery, achievements, streaks and challenge progress.
        var workoutLogId = await sender.Send(new LogWorkoutCommand(memberId, null, request.Entries), cancellationToken);

        var (xpAfter, levelAfter) = await ReadProgressionAsync(memberId, cancellationToken);
        var workoutDates = await WorkoutDatesAsync(memberId, zone, cancellationToken);
        var sessionsAfter = WeeklyGoalPolicy.SessionsThisWeek(workoutDates, today);

        var goal = await db.MemberTrainingPreferences.AsNoTracking()
            .Where(p => p.MemberId == memberId)
            .Select(p => (int?)p.WeeklySessionGoal)
            .FirstOrDefaultAsync(cancellationToken) ?? WeeklyGoalPolicy.DefaultWeeklySessionGoal;

        // Records carry the id of the log that set them, so "what did THIS session beat" is a lookup
        // rather than a guess from timestamps.
        // PersonalRecord carries no Exercise navigation, so join to the tenant-scoped exercise catalog
        // for the name the member actually recognises.
        var newRecords = await (from r in db.PersonalRecords.AsNoTracking()
                                join e in db.Exercises.AsNoTracking() on r.ExerciseId equals e.Id
                                where r.MemberId == memberId && r.WorkoutLogId == workoutLogId
                                select new MyNewRecordDto(e.Name, r.Type.ToString(), r.Value))
            .ToListAsync(cancellationToken);

        var newAchievements = (await db.MemberAchievements.AsNoTracking()
                .Where(a => a.MemberId == memberId && !achievementsBefore.Contains(a.Code))
                .Select(a => a.Code)
                .ToListAsync(cancellationToken))
            .Select(code => AchievementCatalog.All.FirstOrDefault(d => d.Code == code))
            .Where(d => d is not null)
            .Select(d => new MyNewAchievementDto(d!.Code, d.Name, d.Description, d.Tier.ToString(), d.Icon))
            .ToList();

        var challengeProgress = (await sender.Send(new GetMyChallengesQuery(), cancellationToken))
            .Where(c => c.Joined)
            .Select(c => new MyChallengeStepDto(
                c.Name, c.MyWorkoutCount, c.TargetWorkoutCount,
                c.IsCompleted && !challengesBefore.GetValueOrDefault(c.Id)))
            .ToList();

        return new MyWorkoutResultDto(
            workoutLogId,
            character,
            (int)(xpAfter - xpBefore),
            levelAfter,
            levelAfter > levelBefore,
            sessionsAfter,
            goal,
            WeeklyGoalPolicy.IsGoalMet(sessionsAfter, goal),
            // "Just met" is the celebration-worthy transition; GoalMet alone stays true for every
            // bonus session afterwards, and re-congratulating someone every time gets hollow fast.
            WeeklyGoalPolicy.IsGoalMet(sessionsAfter, goal) && !WeeklyGoalPolicy.IsGoalMet(sessionsBefore, goal),
            StreakCalculator.CurrentWeeklyStreak(workoutDates, today),
            newRecords,
            newAchievements,
            challengeProgress);
    }

    /// <summary>Total XP and level, or (0, 1) for a member who has never earned any.</summary>
    private async Task<(long Xp, int Level)> ReadProgressionAsync(Guid memberId, CancellationToken cancellationToken)
    {
        var row = await db.MemberProgressions.AsNoTracking()
            .Where(p => p.MemberId == memberId)
            .Select(p => new { p.TotalXp, p.Level })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? (0L, 1) : (row.TotalXp, row.Level);
    }

    private async Task<List<DateOnly>> WorkoutDatesAsync(Guid memberId, TimeZoneInfo zone, CancellationToken cancellationToken)
        => (await db.WorkoutLogs.AsNoTracking()
                .Where(w => w.MemberId == memberId)
                .Select(w => w.LoggedAt)
                .ToListAsync(cancellationToken))
            .Select(d => GymDay.Of(d, zone))
            .ToList();
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
        var zone = await MyMemberResolver.ResolveGymZoneAsync(db, memberId, cancellationToken);
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
        var zone = await MyMemberResolver.ResolveGymZoneAsync(db, memberId, cancellationToken);
        var today = GymDay.Of(dateTimeProvider.UtcNow, zone);

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
        var zone = await MyMemberResolver.ResolveGymZoneAsync(db, memberId, cancellationToken);
        var today = GymDay.Of(dateTimeProvider.UtcNow, zone);

        return await sender.Send(
            new AddMeasurementCommand(
                memberId, today, request.WeightKg, request.BodyFatPercentage, request.ChestCm,
                request.WaistCm, request.HipCm, request.ArmCm, request.ThighCm, request.Notes),
            cancellationToken);
    }
}
