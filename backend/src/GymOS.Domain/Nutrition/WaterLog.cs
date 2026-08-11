using GymOS.Domain.Common;

namespace GymOS.Domain.Nutrition;

public class WaterLog : BaseEntity, ITenantScoped
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

    public int AmountMl { get; set; }

    public DateTimeOffset LoggedAt { get; set; }
}
