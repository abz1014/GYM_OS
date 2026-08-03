using GymOS.Domain.Common;

namespace GymOS.Domain.Tenancy;

public class Branch : BaseEntity, ITenantScoped, IAuditable
{
    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string AddressLine { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public string TimeZone { get; set; } = "UTC";

    /// <summary>ISO 4217 currency code. Overridable per-plan/invoice; branch value is the default.</summary>
    public string Currency { get; set; } = "USD";

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    public Tenant? Tenant { get; set; }
}
