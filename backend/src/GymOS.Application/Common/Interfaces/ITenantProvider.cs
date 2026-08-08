namespace GymOS.Application.Common.Interfaces;

/// <summary>
/// The narrow tenant/branch context EF Core's global query filters depend on. Kept separate from
/// ICurrentUserService so GymOsDbContext only needs to know about this, not the whole user model.
/// </summary>
public interface ITenantProvider
{
    Guid? TenantId { get; }

    Guid? BranchId { get; }

    /// <summary>
    /// The branches this caller may touch, or <c>null</c> when branch scoping does not apply.
    ///
    /// Null is the SYSTEM context — background jobs, the seeder, and anything running without an
    /// HTTP request. Those legitimately operate across every branch, and a job that silently saw
    /// nothing would be a far quieter failure than one that saw too much.
    ///
    /// An empty list is not the same thing: it means a real user who has been granted no branches,
    /// and they should see nothing. Modelling "no restriction" as an empty list would have collapsed
    /// those two cases into the dangerous one.
    /// </summary>
    IReadOnlyList<Guid>? AccessibleBranchIds { get; }
}
