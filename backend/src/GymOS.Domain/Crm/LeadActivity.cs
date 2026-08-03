using GymOS.Domain.Common;

namespace GymOS.Domain.Crm;

public class LeadActivity : BaseEntity
{
    public Guid LeadId { get; set; }

    public Lead? Lead { get; set; }

    public LeadActivityType Type { get; set; }

    public string Notes { get; set; } = string.Empty;

    public DateTimeOffset? DueDate { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }
}
