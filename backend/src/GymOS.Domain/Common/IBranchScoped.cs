namespace GymOS.Domain.Common;

/// <summary>
/// Marks an entity as belonging to one branch (location) within a tenant. Branch is the unit
/// the UI's branch switcher operates on; Tenant stays invisible in the UI.
/// </summary>
public interface IBranchScoped : ITenantScoped
{
    Guid BranchId { get; set; }
}
