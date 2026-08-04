using GymOS.Domain.Common;

namespace GymOS.Domain.Experience;

/// <summary>
/// One append-only personal-record row: the member beat their prior best on a metric for an exercise,
/// in a specific workout. Never updated — the current record is the max <see cref="Value"/> for a
/// (member, exercise, type), and the row's <see cref="AchievedAt"/> is when it happened, so the whole
/// PR history (and "when did I hit this") survives. Not <see cref="IAuditable"/>: written by a
/// domain-event handler with no HTTP user in scope, so it carries its own AchievedAt.
/// </summary>
public class PersonalRecord : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid MemberId { get; set; }

    public Guid ExerciseId { get; set; }

    public PersonalRecordType Type { get; set; }

    public decimal Value { get; set; }

    /// <summary>The workout the record was set in — lets the timeline (later slice) deep-link to it.</summary>
    public Guid? WorkoutLogId { get; set; }

    public DateTimeOffset AchievedAt { get; set; }
}
