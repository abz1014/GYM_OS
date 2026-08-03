namespace GymOS.Application.Modules.Auth.Dtos;

public record CurrentUserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool MfaEnabled,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<Guid> AccessibleBranchIds);

public record AuthResultDto(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    CurrentUserDto User);
