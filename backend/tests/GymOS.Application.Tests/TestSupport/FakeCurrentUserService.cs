using GymOS.Application.Common.Interfaces;

namespace GymOS.Application.Tests.TestSupport;

/// <summary>
/// Stands in for the JWT-claims-derived ICurrentUserService in tests. Settable per-test so a
/// single fixture instance can simulate "anonymous request", "authenticated user", and
/// "switch to a different tenant" within the same test class.
/// </summary>
public class FakeCurrentUserService : ICurrentUserService
{
    public Guid? TenantId { get; set; }

    public Guid? BranchId { get; set; }

    public Guid? UserId { get; set; }

    public string? Email { get; set; }

    public IReadOnlyList<string> Roles { get; set; } = [];

    public IReadOnlyList<string> Permissions { get; set; } = [];

    public bool IsAuthenticated { get; set; }

    public bool HasPermission(string permissionCode) => Permissions.Contains(permissionCode);
}
