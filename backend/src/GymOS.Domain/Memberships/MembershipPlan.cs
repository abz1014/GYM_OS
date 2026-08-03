using GymOS.Domain.Common;

namespace GymOS.Domain.Memberships;

public class MembershipPlan : BaseEntity, ITenantScoped, IAuditable
{
    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public MembershipPlanType Type { get; set; }

    public string? Description { get; set; }

    public int DurationDays { get; set; }

    public decimal Price { get; set; }

    public string Currency { get; set; } = string.Empty;

    public int MaxFreezeDays { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    public ICollection<Discount> Discounts { get; set; } = [];
}
