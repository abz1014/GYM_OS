using GymOS.Domain.Common;

namespace GymOS.Domain.Tenancy;

public class Branch : BaseEntity, ITenantScoped, IAuditable
{
    /// <summary>Not a recommendation — just the point past which a capacity is a typo rather than a
    /// building. Deliberately far above any real site, since its only job is to stop a slipped digit
    /// silently becoming the denominator every occupancy reading is divided by.</summary>
    public const int MaxCapacity = 100_000;

    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string AddressLine { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public string TimeZone { get; set; } = "UTC";

    /// <summary>ISO 4217 currency code. Overridable per-plan/invoice; branch value is the default.</summary>
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// How many people this site holds, as the gym itself defines it — usually a fire-safety or
    /// licence figure, not something derivable from the floor plan.
    ///
    /// Nullable, and that is the point: "nobody has told us" is a real state and a very common one,
    /// since capacity is a number an owner has to go and look up. Every surface that shows a "34 of
    /// 180" or a fill bar needs this as its denominator and must fall back to the bare count without
    /// it — a default here would be a made-up ceiling that makes every reading wrong in the same
    /// direction, and worse, wrong invisibly.
    /// </summary>
    public int? Capacity { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    public Tenant? Tenant { get; set; }
}
