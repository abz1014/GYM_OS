using GymOS.Application.Common.Interfaces;
using GymOS.Domain.Workouts;
using Microsoft.EntityFrameworkCore;

namespace GymOS.Application.Common.Coaching;

/// <summary>
/// The session a coach message is about, resolved for display.
/// </summary>
/// <param name="Label">What to call it: the trainer's own template name when there is one, since
/// their words beat a derived label, otherwise the character SessionCharacterPolicy names it.</param>
/// <param name="ExerciseCount">Distinct exercises, not entries — five sets of squats is one exercise,
/// and counting rows would make a focused session look sprawling.</param>
public record CoachMessageSessionDto(Guid Id, string Label, DateTimeOffset LoggedAt, int ExerciseCount);

/// <summary>
/// Turns the WorkoutLogId a message carries into something a person can read.
///
/// This lives in Common because both halves of the conversation need exactly the same answer — the
/// member reading "about your Tuesday session" and the trainer who wrote it must see the same label
/// for the same workout, and two implementations would eventually disagree about a template rename
/// or a session with no muscle groups on it.
///
/// Batched by design: a thread of twenty messages referencing sessions would otherwise be twenty
/// round trips, and the conversation queries already load their window in one go.
/// </summary>
public static class CoachMessageSessions
{
    /// <summary>
    /// Looks up every referenced session at once. Ids that no longer resolve are simply absent from
    /// the result rather than throwing — a workout can be deleted (the member's own undo does exactly
    /// that) while the message about it survives, and losing the whole conversation because one
    /// attachment went away would be the wrong failure.
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, CoachMessageSessionDto>> ResolveAsync(
        IApplicationDbContext db, IEnumerable<Guid> workoutLogIds, CancellationToken cancellationToken)
    {
        var ids = workoutLogIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, CoachMessageSessionDto>();
        }

        var logs = await db.WorkoutLogs.AsNoTracking()
            .Where(w => ids.Contains(w.Id))
            .Select(w => new
            {
                w.Id,
                w.LoggedAt,
                w.WorkoutTemplateId,
                ExerciseIds = w.Entries.Select(e => e.ExerciseId).ToList(),
            })
            .ToListAsync(cancellationToken);

        // Separate lookups rather than navigation properties, because WorkoutLog has none — the same
        // shape GetMemberWorkoutLogsQuery uses, which is also why this cannot simply reuse that query.
        var templateIds = logs.Where(l => l.WorkoutTemplateId is not null)
            .Select(l => l.WorkoutTemplateId!.Value).Distinct().ToList();

        var templateNames = templateIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.WorkoutTemplates.AsNoTracking()
                .Where(t => templateIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);

        var exerciseIds = logs.SelectMany(l => l.ExerciseIds).Distinct().ToList();
        var muscleGroups = exerciseIds.Count == 0
            ? new Dictionary<Guid, string?>()
            : await db.Exercises.AsNoTracking()
                .Where(e => exerciseIds.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id, e => (string?)e.MuscleGroup, cancellationToken);

        return logs.ToDictionary(
            w => w.Id,
            w =>
            {
                var templateName = w.WorkoutTemplateId is Guid tid ? templateNames.GetValueOrDefault(tid) : null;

                return new CoachMessageSessionDto(
                    w.Id,
                    // The trainer's own name for the block when there is one; otherwise what the
                    // session actually looks like, by the same rule the member sees on their history.
                    string.IsNullOrWhiteSpace(templateName)
                        // One per ENTRY, matching GetMemberWorkoutLogsQuery: repeats decide which group
                        // led the session, so the same workout gets the same name on both screens.
                        ? SessionCharacterPolicy.Describe(w.ExerciseIds.Select(muscleGroups.GetValueOrDefault))
                        : templateName!,
                    w.LoggedAt,
                    // Distinct: five sets of squats is one exercise, and counting rows would make a
                    // focused session look sprawling.
                    w.ExerciseIds.Distinct().Count());
            });
    }
}
