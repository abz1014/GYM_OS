using GymOS.Domain.Common;

namespace GymOS.Domain.Workouts;

public class WorkoutTemplateExercise : BaseEntity
{
    public Guid WorkoutTemplateId { get; set; }

    public WorkoutTemplate? WorkoutTemplate { get; set; }

    public Guid ExerciseId { get; set; }

    public Exercise? Exercise { get; set; }

    public int SetsCount { get; set; }

    public int RepsCount { get; set; }

    public int OrderIndex { get; set; }
}
