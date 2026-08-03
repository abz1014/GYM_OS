using GymOS.Domain.Common;

namespace GymOS.Domain.Crm;

public class Lead : BaseEntity, IBranchScoped, IAuditable
{
    public Guid TenantId { get; set; }

    public Guid BranchId { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string FullName => $"{FirstName} {LastName}";

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public LeadSource Source { get; set; }

    public LeadStage Stage { get; set; } = LeadStage.Lead;

    public Guid? AssignedToUserId { get; set; }

    public Guid? ConvertedMemberId { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    public ICollection<LeadActivity> Activities { get; set; } = [];
}
