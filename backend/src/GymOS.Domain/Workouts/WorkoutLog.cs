using GymOS.Domain.Common;

namespace GymOS.Domain.Workouts;

public class WorkoutLog : BaseEntity
{
    public Guid MemberId { get; set; }

    public Guid? WorkoutTemplateId { get; set; }

    public DateTimeOffset LoggedAt { get; set; }

    public ICollection<WorkoutLogEntry> Entries { get; set; } = [];
}
