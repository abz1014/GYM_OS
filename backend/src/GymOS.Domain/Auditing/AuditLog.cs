using GymOS.Domain.Common;

namespace GymOS.Domain.Auditing;

public class AuditLog : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid? UserId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public Guid EntityId { get; set; }

    public string? DataBefore { get; set; }

    public string? DataAfter { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}
