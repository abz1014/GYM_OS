using GymOS.Domain.Common;

namespace GymOS.Domain.Workouts;

public class WorkoutLog : AggregateRoot
{
    public Guid MemberId { get; set; }

    public Guid? WorkoutTemplateId { get; set; }

    /// <summary>The plan this session came from, when it came from one. Null for a self-logged
    /// workout, which is most of them.</summary>
    public WorkoutTemplate? WorkoutTemplate { get; set; }

    public DateTimeOffset LoggedAt { get; set; }

    public ICollection<WorkoutLogEntry> Entries { get; set; } = [];

    /// <summary>Signals the Member Experience Engine that this workout was logged. Called by the
    /// command handler after the log is populated; dispatched by GymOsDbContext after save.</summary>
    public void RaiseLogged() => AddDomainEvent(new WorkoutLoggedEvent(MemberId, Id));
}
