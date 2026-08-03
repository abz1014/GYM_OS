using GymOS.Domain.Common;
using GymOS.Domain.Tenancy;

namespace GymOS.Domain.Identity;

public class UserBranchAccess : BaseEntity
{
    public Guid UserId { get; set; }

    public User? User { get; set; }

    public Guid BranchId { get; set; }

    public Branch? Branch { get; set; }
}
