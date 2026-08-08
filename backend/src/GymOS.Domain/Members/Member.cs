using GymOS.Domain.Common;
using GymOS.Domain.Identity;

namespace GymOS.Domain.Members;

public class Member : BaseEntity, IBranchScoped, IAuditable, ISoftDelete
{
    public Guid TenantId { get; set; }

    public Guid BranchId { get; set; }

    public Guid? UserId { get; set; }

    public User? User { get; set; }

    public string MemberCode { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string FullName => $"{FirstName} {LastName}";

    public DateOnly? DateOfBirth { get; set; }

    public string? Gender { get; set; }

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Address { get; set; }

    public string? ProfilePhotoUrl { get; set; }

    public DateOnly JoinDate { get; set; }

    /// <summary>
    /// The existing member who brought this one in, when the gym knows it. Powers the referral
    /// surfaces (top referrers for staff, "friends you brought in" for the member) — word of mouth
    /// is most gyms' #1 acquisition channel and this makes it measurable.
    /// </summary>
    public Guid? ReferredByMemberId { get; set; }

    public Member? ReferredByMember { get; set; }

    public MemberStatus Status { get; set; } = MemberStatus.Active;

    public string QrCodeToken { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public ICollection<EmergencyContact> EmergencyContacts { get; set; } = [];

    public ICollection<MedicalNote> MedicalNotes { get; set; } = [];

    /// <summary>Operational staff notes. Distinct from MedicalNotes — see <see cref="MemberNote"/>.</summary>
    public ICollection<MemberNote> Notes { get; set; } = [];

    public ICollection<MemberMeasurement> Measurements { get; set; } = [];

    public ICollection<ProgressPhoto> ProgressPhotos { get; set; } = [];

    public ICollection<MemberMembership> MemberMemberships { get; set; } = [];
}
