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

/// <param name="RoleName">The staff member's single role. Empty only for a user carrying no UserRole
/// row at all — a shape nothing in the product creates, but one the list must render rather than
/// throw on, since a staff screen that 500s is the one place you cannot go to fix it.</param>
/// <param name="LastLoginAt">Null for an account that has never been signed into — usually one
/// created minutes ago whose temporary password has not been handed over yet. Not "never used".</param>
public record StaffMemberDto(
    Guid Id, string Email, string FirstName, string LastName, string? Phone, bool IsActive,
    string RoleName, IReadOnlyList<Guid> BranchIds, DateTimeOffset? LastLoginAt);

/// <summary>The staff screen's whole payload: the roster plus the roles it is allowed to assign, so
/// the role dropdown never has to be a hardcoded copy of RoleNames in the frontend.</summary>
public record StaffListDto(IReadOnlyList<StaffMemberDto> Staff, IReadOnlyList<RoleDto> Roles);

/// <param name="TemporaryPassword">Shown once, to be read out to the new hire. Never stored in the
/// clear and never recoverable — a lost one is replaced through reset-password, not looked up.</param>
public record CreateStaffResultDto(Guid Id, string TemporaryPassword);

public record ResetStaffPasswordResultDto(string TemporaryPassword);
