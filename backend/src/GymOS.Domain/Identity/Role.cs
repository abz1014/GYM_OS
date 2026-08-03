using GymOS.Domain.Common;

namespace GymOS.Domain.Identity;

public class Role : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsSystemRole { get; set; } = true;

    public ICollection<RolePermission> RolePermissions { get; set; } = [];

    public ICollection<UserRole> UserRoles { get; set; } = [];
}
