using GymOS.Domain.Common;

namespace GymOS.Domain.Tenancy;

/// <summary>
/// A gym company/customer of GymOS. Deliberately never surfaced in the UI for the single-client
/// MVP — it exists purely so the schema can scale to real multi-tenant SaaS later without
/// touching business logic.
/// </summary>
public class Tenant : BaseEntity, IAuditable
{
    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    public ICollection<Branch> Branches { get; set; } = [];
}
