using GymOS.Domain.Common;

namespace GymOS.Domain.Workouts;

public class WorkoutLogEntry : BaseEntity
{
    public Guid WorkoutLogId { get; set; }

    public WorkoutLog? WorkoutLog { get; set; }

    public Guid ExerciseId { get; set; }

    /// <summary>The movement this entry records.</summary>
    public Exercise? Exercise { get; set; }

    public int SetsCompleted { get; set; }

    public int RepsCompleted { get; set; }

    public decimal? WeightKg { get; set; }
}
