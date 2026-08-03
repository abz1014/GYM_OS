using GymOS.Domain.Common;

namespace GymOS.Domain.Identity;

public class Permission : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string Module { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}
