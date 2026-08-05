using GymOS.Domain.Common;
using GymOS.Domain.Workouts;

namespace GymOS.Domain.Experience;

/// <summary>
/// One step in a <see cref="SkillTree"/>: an exercise plus the rep threshold that "unlocks" it and a
/// hand-authored explanation of why it's the next step. Not itself tenant-scoped (mirrors
/// WorkoutTemplateExercise's convention) — scoped through its parent SkillTree.
/// </summary>
public class SkillNode : BaseEntity
{
    public Guid SkillTreeId { get; set; }

    public SkillTree? SkillTree { get; set; }

    public Guid ExerciseId { get; set; }

    public Exercise? Exercise { get; set; }

    /// <summary>Position within the tree, ascending — the progression order.</summary>
    public int OrderIndex { get; set; }

    /// <summary>The best single-set rep count on this exercise that counts as "mastered" it.</summary>
    public int MinReps { get; set; }

    public string UnlockExplanation { get; set; } = string.Empty;
}
