using GymOS.Domain.Common;

namespace GymOS.Domain.Settings;

public class SystemPreference : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid? BranchId { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string? Description { get; set; }
}
