using GymOS.Domain.Common;

namespace GymOS.Domain.Settings;

public class GymProfile : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public string LegalName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? LogoUrl { get; set; }

    public string? TaxId { get; set; }

    public string? SupportEmail { get; set; }

    public string? SupportPhone { get; set; }

    public string DefaultCurrency { get; set; } = string.Empty;

    public string DefaultTimeZone { get; set; } = string.Empty;
}
