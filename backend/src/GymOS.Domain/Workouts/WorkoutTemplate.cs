using GymOS.Domain.Common;

namespace GymOS.Domain.Workouts;

public class WorkoutTemplate : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public ICollection<WorkoutTemplateExercise> TemplateExercises { get; set; } = [];
}
