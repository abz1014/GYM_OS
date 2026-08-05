namespace GymOS.Domain.Experience;

/// <summary>
/// The kind of source record that triggered an <see cref="XpTransaction"/>. Paired with a nullable
/// SourceId it gives every award a stable, idempotent key: the same (SourceType, SourceId, Reason)
/// is never awarded twice, so a re-published domain event or a job retry can't double-credit.
/// </summary>
public enum XpSourceType
{
    WorkoutLog,
    Attendance,
    MemberGoal,
    MealEntry,
    RecoveryLog,
    Challenge,
    Manual
}
