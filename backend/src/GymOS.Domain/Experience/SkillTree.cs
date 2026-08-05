using GymOS.Domain.Common;

namespace GymOS.Domain.Experience;

/// <summary>
/// A catalog of exercise progressions ("SkillTree", blueprint Phase 7) — e.g. Leg Press → Barbell
/// Squat → Deadlift. Purely advisory: a member is never blocked from any exercise or equipment
/// regardless of tree progress ("never locks equipment") — it only powers ExerciseSubstitution
/// recommendations. Tenant-scoped like Exercise/WorkoutTemplate, since its nodes reference the
/// tenant's own exercise catalog.
/// </summary>
public class SkillTree : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? MuscleGroup { get; set; }

    public ICollection<SkillNode> Nodes { get; set; } = [];
}
