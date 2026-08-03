namespace GymOS.Application.Modules.Settings.Dtos;

public record BranchDto(Guid Id, string Name, string AddressLine, string City, string Country, string TimeZone, string Currency, bool IsActive);

public record GymProfileDto(string LegalName, string DisplayName, string? LogoUrl, string? SupportEmail, string? SupportPhone, string DefaultCurrency, string DefaultTimeZone);

public record RoleDto(Guid Id, string Name);

public record PermissionCatalogEntryDto(Guid Id, string Code, string Module, string Description);

public record RolePermissionGrantDto(Guid RoleId, Guid PermissionId);

public record PermissionMatrixDto(
    IReadOnlyList<RoleDto> Roles, IReadOnlyList<PermissionCatalogEntryDto> Permissions, IReadOnlyList<RolePermissionGrantDto> Grants);

public record SystemPreferenceDto(Guid Id, Guid? BranchId, string Key, string Value, string? Description);
