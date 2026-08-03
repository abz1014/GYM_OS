using GymOS.Domain.Common;

namespace GymOS.Domain.Members;

public class EmergencyContact : BaseEntity
{
    public Guid MemberId { get; set; }

    public Member? Member { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Relationship { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string? Email { get; set; }
}
