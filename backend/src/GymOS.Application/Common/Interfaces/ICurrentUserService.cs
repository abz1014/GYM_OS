namespace GymOS.Application.Common.Interfaces;

public interface ICurrentUserService : ITenantProvider
{
    Guid? UserId { get; }

    string? Email { get; }

    IReadOnlyList<string> Roles { get; }

    IReadOnlyList<string> Permissions { get; }

    bool IsAuthenticated { get; }

    bool HasPermission(string permissionCode);
}
