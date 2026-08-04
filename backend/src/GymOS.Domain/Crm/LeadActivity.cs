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

    /// <summary>
    /// When this activity was logged. A plain field (not IAuditable) set explicitly by the command
    /// handler and the seeder, rather than the audit interceptor — the interceptor stamps
    /// CreatedByUserId from the ambient JWT on every Added entity, which would silently null out the
    /// deliberately-attributed staff id the seeder sets when running outside a request context.
    /// Drives both LeadScorePolicy's recency signal and LeadDripPolicy's "has anyone touched this
    /// lead yet" check.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
