namespace GymOS.Domain.Common;

/// <summary>
/// Marks an entity as belonging to a single tenant (gym company). Enforced via an EF Core
/// global query filter in GymOsDbContext so every query is automatically scoped.
/// </summary>
public interface ITenantScoped
{
    Guid TenantId { get; set; }
}
