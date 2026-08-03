using GymOS.Domain.Common;
using GymOS.Domain.Members;

namespace GymOS.Domain.Trainers;

public class TrainerRating : BaseEntity
{
    public Guid TrainerId { get; set; }

    public Trainer? Trainer { get; set; }

    public Guid MemberId { get; set; }

    public Member? Member { get; set; }

    public int Score { get; set; }

    public string? Comment { get; set; }

    public DateTimeOffset RatedAt { get; set; }
}
