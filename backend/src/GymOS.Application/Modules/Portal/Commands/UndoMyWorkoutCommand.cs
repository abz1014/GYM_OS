using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Experience.Services;
using GymOS.Domain.Workouts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Portal.Commands;

/// <summary>
/// Takes back a session the member just recorded.
///
/// One-tap confirmation is what makes this necessary. A mis-tap costs nothing to make and a great
/// deal to leave standing: it mints XP, can register a personal record that never happened, and
/// inflates the streak the whole member experience rests on. Without undo, the fastest way to log is
/// also the fastest way to corrupt the numbers that make logging worth doing.
///
/// Unwinds what the session created rather than marking it cancelled, because a hidden-but-present
/// workout would still be counted by every projection that reads the log table. XP awarded for it and
/// records set by it are removed, and mastery is recomputed from what remains.
///
/// Achievements are deliberately NOT revoked. They are milestones a member has already been shown and
/// celebrated; taking a badge back over a mis-tap is a worse experience than letting an early one
/// stand, and unlike XP or a personal record it does not distort any ranking. The next evaluation
/// re-derives them from real history anyway.
///
/// Ownership is the security boundary: another member's log id answers 404, never 403, so its
/// existence is not confirmed — the same convention as CancelMyClassBookingCommand.
/// </summary>
public record UndoMyWorkoutCommand(Guid WorkoutLogId) : ICommand<Unit>;

public class UndoMyWorkoutCommandHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    IWorkoutProgressionService workoutProgression)
    : IRequestHandler<UndoMyWorkoutCommand, Unit>
{
    public async Task<Unit> Handle(UndoMyWorkoutCommand request, CancellationToken cancellationToken)
    {
        var memberId = await MyMemberResolver.ResolveMemberIdAsync(db, currentUser, cancellationToken);
        var now = dateTimeProvider.UtcNow;

        var log = await db.WorkoutLogs
            .Include(w => w.Entries)
            .FirstOrDefaultAsync(w => w.Id == request.WorkoutLogId, cancellationToken)
            ?? throw new NotFoundException(nameof(WorkoutLog), request.WorkoutLogId);

        if (log.MemberId != memberId)
        {
            throw new NotFoundException(nameof(WorkoutLog), request.WorkoutLogId);
        }

        if (!WorkoutUndoPolicy.CanUndo(log.LoggedAt, now))
        {
            // Undo is for a mis-tap noticed immediately, not for editing history — a member must not
            // be able to quietly rewrite last week to move a leaderboard.
            throw new ForbiddenAccessException(
                $"A workout can only be undone within {WorkoutUndoPolicy.UndoWindow.TotalMinutes:0} minutes of being recorded.");
        }

        var tenantId = currentUser.TenantId ?? throw new ForbiddenAccessException("No tenant context.");
        var affectedExerciseIds = log.Entries.Select(e => e.ExerciseId).Distinct().ToList();

        // XP awarded FOR this session. SourceId is the log id, so this removes exactly what it earned
        // and leaves every other award — visits, meals, recovery — untouched.
        var xp = await db.XpTransactions
            .Where(t => t.MemberId == memberId && t.SourceId == log.Id)
            .ToListAsync(cancellationToken);
        db.XpTransactions.RemoveRange(xp);

        // Records set BY this session. PersonalRecord carries the log id, so no guesswork.
        var records = await db.PersonalRecords
            .Where(r => r.MemberId == memberId && r.WorkoutLogId == log.Id)
            .ToListAsync(cancellationToken);
        db.PersonalRecords.RemoveRange(records);

        db.WorkoutLogs.Remove(log);

        // Persist the removals before recomputing: mastery is rebuilt from the member's remaining
        // history, so the deleted entries must actually be gone before it reads them.
        await db.SaveChangesAsync(cancellationToken);

        foreach (var exerciseId in affectedExerciseIds)
        {
            await workoutProgression.RecomputeMasteryAsync(tenantId, memberId, exerciseId, now, cancellationToken);
        }

        // Level follows the ledger that is left, so removing the XP above is enough to walk it back.
        var progression = await db.MemberProgressions.FirstOrDefaultAsync(p => p.MemberId == memberId, cancellationToken);
        if (progression is not null)
        {
            var remaining = (await db.XpTransactions.AsNoTracking()
                    .Where(t => t.MemberId == memberId)
                    .Select(t => t.Amount)
                    .ToListAsync(cancellationToken))
                .Sum();
            progression.SetTotalXp(remaining);
            progression.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
