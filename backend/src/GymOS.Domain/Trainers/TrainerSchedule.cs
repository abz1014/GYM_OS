using GymOS.Domain.Common;

namespace GymOS.Domain.Trainers;

public class TrainerSchedule : BaseEntity
{
    public Guid TrainerId { get; set; }

    public Trainer? Trainer { get; set; }

    public DayOfWeek DayOfWeek { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public bool IsAvailable { get; set; } = true;
}
