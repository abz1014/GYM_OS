using GymOS.Domain.Common;
using GymOS.Domain.Identity;

namespace GymOS.Domain.Trainers;

public class Trainer : BaseEntity, IBranchScoped
{
    public Guid TenantId { get; set; }

    public Guid BranchId { get; set; }

    public Guid UserId { get; set; }

    public User? User { get; set; }

    public string Specialties { get; set; } = string.Empty;

    public decimal CommissionRate { get; set; }

    public string? Bio { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<TrainerAssignment> Assignments { get; set; } = [];

    public ICollection<TrainerSchedule> Schedules { get; set; } = [];

    public ICollection<TrainerRating> Ratings { get; set; } = [];

    public ICollection<CommissionRecord> CommissionRecords { get; set; } = [];
}
