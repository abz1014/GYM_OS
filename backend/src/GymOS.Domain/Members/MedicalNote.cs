using GymOS.Domain.Common;

namespace GymOS.Domain.Members;

public class MedicalNote : BaseEntity
{
    public Guid MemberId { get; set; }

    public Member? Member { get; set; }

    public string Note { get; set; } = string.Empty;

    public Guid? RecordedByUserId { get; set; }

    public DateTimeOffset RecordedAt { get; set; }
}
