using GymOS.Application.Common.Exceptions;
using GymOS.Application.Common.Interfaces;
using GymOS.Application.Common.Messaging;
using GymOS.Application.Modules.Experience.Services;
using GymOS.Domain.Experience;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Modules.Experience.Commands;

/// <summary>
/// Recomputes the Member Experience Engine's two DB-persisted, incrementally-maintained projections —
/// MemberProgression (TotalXp/Level) and ExerciseMastery — from their append-only sources
/// (XpTransaction ledger, WorkoutLogEntries), for every member in the tenant. This is the "one-shot
/// rebuild command" the design calls for: a rule change to XpPolicy or the mastery reduction never
/// needs a destructive migration, just a rerun of this command.
///
/// Deliberately does NOT touch XpTransaction, PersonalRecord, or MemberAchievement rows — those are
/// themselves append-only ledgers (the source of truth), not projections derived from something else.
/// Recovery status, nutrition adherence, recommendations, and the transformation timeline need no
/// rebuild step at all: they're computed fresh from source data on every request and were never
/// stored in the first place.
///
/// Finishes with an achievement backfill pass (IAchievementService.EvaluateAsync per member) — a
/// corrected level or mastery total can newly satisfy a rule that was unmet before, and that
/// evaluation only ever inserts missing rows, never removes or edits existing unlocks.
///
/// Idempotent by construction: MemberProgression.SetTotalXp and the mastery/achievement upserts all
/// converge to the same result no matter how many times this runs.
/// </summary>
public record RebuildExperienceProjectionsCommand : ICommand<RebuildExperienceProjectionsResult>;

public record RebuildExperienceProjectionsResult(
    int MembersConsidered, int ProgressionsRebuilt, int MasteryRowsRebuilt, int AchievementsBackfilled);

public class RebuildExperienceProjectionsCommandHandler(
    IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider,
    IWorkoutProgressionService workoutProgression, IAchievementService achievements)
    : IRequestHandler<RebuildExperienceProjectionsCommand, RebuildExperienceProjectionsResult>
{
    public async Task<RebuildExperienceProjectionsResult> Handle(
        RebuildExperienceProjectionsCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new ForbiddenAccessException("No tenant context.");
        var now = dateTimeProvider.UtcNow;

        var memberIds = await db.Members.AsNoTracking().Select(m => m.Id).ToListAsync(cancellationToken);

        // MemberProgression: sum the XP ledger per member and set the absolute total. SetTotalXp
        // (not AddXp) is the rebuild-safe entry point — it recomputes Level but raises no change
        // event, so this pass can never trigger a mid-rebuild achievement re-evaluation on stale data.
        var xpByMember = (await db.XpTransactions.AsNoTracking()
                .Select(t => new { t.MemberId, t.Amount })
                .ToListAsync(cancellationToken))
            .GroupBy(t => t.MemberId)
            .ToDictionary(g => g.Key, g => g.Sum(t => (long)t.Amount));

        var progressionByMember = await db.MemberProgressions
            .ToDictionaryAsync(p => p.MemberId, cancellationToken);

        var progressionsRebuilt = 0;
        foreach (var memberId in memberIds)
        {
            var totalXp = xpByMember.GetValueOrDefault(memberId, 0L);
            if (!progressionByMember.TryGetValue(memberId, out var progression))
            {
                if (totalXp == 0)
                {
                    continue; // never earned XP -> no row, matching what the incremental path would leave
                }

                progression = new MemberProgression { TenantId = tenantId, MemberId = memberId };
                db.MemberProgressions.Add(progression);
            }

            // RebuildTo, not SetTotalXp: this is the one caller permitted to lower a level. SetTotalXp
            // ratchets the peak so an undo can never demote anybody, and that same ratchet would make
            // an INFLATED projection permanent — the drift would survive the tool built to repair it.
            progression.RebuildTo(totalXp);
            progression.UpdatedAt = now;
            progressionsRebuilt++;
        }

        // ExerciseMastery: recompute every (member, exercise) pair this tenant has ever logged, via
        // the exact same reduction the incremental per-workout path uses. The memberIds bound is
        // load-bearing, not an optimisation: WorkoutLog/WorkoutLogEntry are scoped through their
        // member rather than carrying a TenantId, so they have NO global tenant query filter — an
        // unbounded query here would sweep in every other tenant's pairs and stamp mastery rows for
        // their members with THIS tenant's id.
        var trainedPairs = await db.WorkoutLogEntries.AsNoTracking()
            .Where(e => memberIds.Contains(e.WorkoutLog!.MemberId))
            .Select(e => new { e.WorkoutLog!.MemberId, e.ExerciseId })
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var pair in trainedPairs)
        {
            await workoutProgression.RecomputeMasteryAsync(tenantId, pair.MemberId, pair.ExerciseId, now, cancellationToken);
        }

        // Persist progression/mastery before evaluating achievements — AchievementService reads
        // Level and mastery-derived stats back via a plain DB query (shared with the event-handler
        // path), so it wouldn't see this pass's rebuilt values while they're still only tracked,
        // unsaved changes.
        await db.SaveChangesAsync(cancellationToken);

        var achievementsBefore = await db.MemberAchievements.CountAsync(cancellationToken);
        foreach (var memberId in memberIds)
        {
            await achievements.EvaluateAsync(memberId, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        var achievementsAfter = await db.MemberAchievements.CountAsync(cancellationToken);

        return new RebuildExperienceProjectionsResult(
            memberIds.Count, progressionsRebuilt, trainedPairs.Count, achievementsAfter - achievementsBefore);
    }
}
