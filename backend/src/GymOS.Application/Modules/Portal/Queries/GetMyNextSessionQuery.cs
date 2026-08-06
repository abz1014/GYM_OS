using GymOS.Application.Common.Interfaces;
using GymOS.Domain.Common;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Portal.Dtos;
using GymOS.Domain.Workouts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Portal.Queries;

/// <summary>
/// The session the app believes the member is about to do (or has just done), pre-filled so
/// recording it is one tap.
///
/// This is the read half of zero-logging. It never writes: the member confirms by posting these
/// entries to the existing log endpoint, which is deliberately the same path a hand-entered workout
/// takes — one place where a session becomes real, one set of rules, one cascade.
///
/// The tier decision and the filling-in live in SessionProposalPolicy; this only gathers what that
/// policy needs. Self-scoped via MyMemberResolver.
/// </summary>
public record GetMyNextSessionQuery : IQuery<MySessionProposalDto>;

public class GetMyNextSessionQueryHandler(
    IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetMyNextSessionQuery, MySessionProposalDto>
{
    public async Task<MySessionProposalDto> Handle(GetMyNextSessionQuery request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);
        var zone = await MyMemberResolver.ResolveGymZoneAsync(db, memberId, cancellationToken);
        var today = GymDay.Of(dateTimeProvider.UtcNow, zone);

        // 1. An active trainer plan, if there is one. Templates carry sets and reps but never load.
        // Exactly one plan is taken — the most recently started. A member moved onto a new block before
        // the old one lapses has two active assignments, and flattening both produces a session no
        // trainer ever wrote, with anything common to them logged twice on a single confirm.
        var activePlans = await db.WorkoutAssignments.AsNoTracking()
            .Where(a => a.MemberId == memberId && a.StartDate <= today && (a.EndDate == null || a.EndDate >= today))
            .Select(a => new { a.Id, a.StartDate, a.WorkoutTemplateId })
            .ToListAsync(cancellationToken);

        var currentPlan = activePlans
            .OrderByDescending(a => a.StartDate).ThenByDescending(a => a.Id)
            .FirstOrDefault();

        // Kept as its own query rather than a SelectMany off the assignment: projecting the assignment's
        // own columns alongside its template rows needs SQL APPLY, which SQLite has no answer for. The
        // trainer's OrderIndex is then applied in memory — EF drops an OrderBy nested inside a
        // SelectMany projection, which handed the session back in arbitrary order, accessory work
        // before the squat.
        var planExercises = new List<PlannedExercise>();
        if (currentPlan is not null)
        {
            planExercises = (await (from e in db.WorkoutTemplateExercises.AsNoTracking()
                                    join x in db.Exercises.AsNoTracking() on e.ExerciseId equals x.Id
                                    where e.WorkoutTemplateId == currentPlan.WorkoutTemplateId
                                    select new { e.OrderIndex, e.ExerciseId, ExerciseName = x.Name, e.SetsCount, e.RepsCount })
                    .ToListAsync(cancellationToken))
                .OrderBy(e => e.OrderIndex)
                .Select(e => new PlannedExercise(e.ExerciseId, e.ExerciseName, e.SetsCount, e.RepsCount))
                .ToList();
        }

        // 2. Everything the member has ever logged, reduced in memory — DateTimeOffset can't be
        //    ordered or bucketed in SQL on SQLite, the same constraint every query here works around.
        // WorkoutLogEntry carries no Exercise navigation, so the tenant-scoped catalogue is joined for
        // the names a member actually recognises — the same shape GetMyTodayQuery uses for records.
        var history = (await (from e in db.WorkoutLogEntries.AsNoTracking()
                              join x in db.Exercises.AsNoTracking() on e.ExerciseId equals x.Id
                              where e.WorkoutLog!.MemberId == memberId
                              select new
                              {
                                  e.WorkoutLogId,
                                  LoggedAt = e.WorkoutLog!.LoggedAt,
                                  e.ExerciseId,
                                  ExerciseName = x.Name,
                                  e.SetsCompleted,
                                  e.RepsCompleted,
                                  e.WeightKg
                              })
                .ToListAsync(cancellationToken))
            .OrderByDescending(e => e.LoggedAt)
            .ToList();

        var lastSession = history
            .GroupBy(e => e.WorkoutLogId)
            .OrderByDescending(g => g.Max(e => e.LoggedAt))
            .Take(1)
            .SelectMany(g => g.Select(e =>
                new ProposedEntry(e.ExerciseId, e.ExerciseName, e.SetsCompleted, e.RepsCompleted, e.WeightKg)))
            .ToList();

        // The load the member last used on each movement — the memory a coach would bring, and the
        // only place a weight for a trainer-prescribed exercise can honestly come from.
        var lastWeightByExercise = history
            .Where(e => e.WeightKg.HasValue)
            .GroupBy(e => e.ExerciseId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.LoggedAt).First().WeightKg!.Value);

        // 3. A starter session for someone with neither, so a first workout is still one tap.
        var starterCatalogue = await db.Exercises.AsNoTracking()
            .OrderBy(e => e.Name)
            .Take(SessionProposalPolicy.StarterExerciseCount)
            .Select(e => new PlannedExercise(e.Id, e.Name, 3, 10))
            .ToListAsync(cancellationToken);

        var proposal = SessionProposalPolicy.Propose(planExercises, lastSession, lastWeightByExercise, starterCatalogue);

        return new MySessionProposalDto(
            proposal.Source.ToString(),
            proposal.CanConfirm,
            proposal.Entries
                .Select(e => new MyProposedEntryDto(e.ExerciseId, e.ExerciseName, e.Sets, e.Reps, e.WeightKg))
                .ToList());
    }
}
