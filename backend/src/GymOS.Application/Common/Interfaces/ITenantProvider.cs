namespace GymOS.Application.Common.Interfaces;

/// <summary>
/// The narrow tenant/branch context EF Core's global query filters depend on. Kept separate from
/// ICurrentUserService so GymOsDbContext only needs to know about this, not the whole user model.
/// </summary>
public interface ITenantProvider
{
    Guid? TenantId { get; }

    Guid? BranchId { get; }
}
