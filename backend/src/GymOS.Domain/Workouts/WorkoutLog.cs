using GymOS.Domain.Common;

namespace GymOS.Domain.Workouts;

public class WorkoutLog : AggregateRoot, ITenantScoped
{
    /// <summary>
    /// Direct tenant scoping, so isolation is a property of the schema rather than of every query
    /// that happens to start from Member.
    ///
    /// This table was reachable only through a tenant-scoped Member, which made it safe in practice
    /// and unguarded in principle: one future query beginning here instead of at Member would cross
    /// tenants silently, with nothing failing. Same class of gap as the cross-branch IDOR, same fix —
    /// enforce it in the model so nobody has to remember.
    /// </summary>
    public Guid TenantId { get; set; }

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
