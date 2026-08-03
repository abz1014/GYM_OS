using GymOS.Domain.Common;

namespace GymOS.Domain.Trainers;

public class TrainerSession : BaseEntity
{
    public Guid TrainerAssignmentId { get; set; }

    public TrainerAssignment? TrainerAssignment { get; set; }

    public DateTimeOffset ScheduledAt { get; set; }

    public int DurationMinutes { get; set; }

    public TrainerSessionStatus Status { get; set; } = TrainerSessionStatus.Scheduled;

    public string? Notes { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
}
