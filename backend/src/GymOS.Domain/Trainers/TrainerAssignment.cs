using GymOS.Domain.Common;
using GymOS.Domain.Members;

namespace GymOS.Domain.Trainers;

public class TrainerAssignment : BaseEntity
{
    public Guid TrainerId { get; set; }

    public Trainer? Trainer { get; set; }

    public Guid MemberId { get; set; }

    public Member? Member { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public bool IsActive { get; set; } = true;
}
