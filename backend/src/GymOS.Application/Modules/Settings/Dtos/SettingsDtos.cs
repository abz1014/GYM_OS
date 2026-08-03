namespace GymOS.Application.Modules.Settings.Dtos;

public record BranchDto(Guid Id, string Name, string City, string Country, string TimeZone, string Currency, bool IsActive);

public record GymProfileDto(string LegalName, string DisplayName, string? LogoUrl, string? SupportEmail, string? SupportPhone, string DefaultCurrency, string DefaultTimeZone);
