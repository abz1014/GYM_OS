using GymOS.Domain.Common;

namespace GymOS.Domain.Identity;

public class User : BaseEntity, ITenantScoped, IAuditable, ISoftDelete
{
    public Guid TenantId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public bool IsActive { get; set; } = true;

    public bool MfaEnabled { get; set; } = false;

    public string? MfaSecret { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = [];

    public ICollection<UserBranchAccess> BranchAccesses { get; set; } = [];

    public string FullName => $"{FirstName} {LastName}";
}
