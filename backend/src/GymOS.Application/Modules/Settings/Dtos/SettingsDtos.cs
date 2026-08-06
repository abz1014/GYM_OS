namespace GymOS.Application.Modules.Settings.Dtos;

/// <param name="Capacity">How many people the site holds, or null when the gym has not said. Null is
/// not zero and must not be rendered as one — see Branch.Capacity.</param>
public record BranchDto(
    Guid Id, string Name, string AddressLine, string City, string Country, string TimeZone, string Currency,
    bool IsActive, int? Capacity);

public record GymProfileDto(string LegalName, string DisplayName, string? LogoUrl, string? SupportEmail, string? SupportPhone, string DefaultCurrency, string DefaultTimeZone);

public record RoleDto(Guid Id, string Name);

public record PermissionCatalogEntryDto(Guid Id, string Code, string Module, string Description);

public record RolePermissionGrantDto(Guid RoleId, Guid PermissionId);

public record PermissionMatrixDto(
    IReadOnlyList<RoleDto> Roles, IReadOnlyList<PermissionCatalogEntryDto> Permissions, IReadOnlyList<RolePermissionGrantDto> Grants);

public record SystemPreferenceDto(Guid Id, Guid? BranchId, string Key, string Value, string? Description);

public record AuditLogDto(
    Guid Id, string Action, string EntityType, Guid EntityId, Guid? UserId, string? UserName, string? DataAfter, DateTimeOffset OccurredAt);
