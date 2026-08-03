using GymOS.Domain.Common;

namespace GymOS.Domain.Members;

public class ProgressPhoto : BaseEntity
{
    public Guid MemberId { get; set; }

    public Member? Member { get; set; }

    public string PhotoUrl { get; set; } = string.Empty;

    public DateTimeOffset TakenAt { get; set; }

    public string? Notes { get; set; }
}
