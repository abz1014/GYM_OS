using GymOS.Domain.Common;

namespace GymOS.Domain.Workouts;

public class Exercise : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? MuscleGroup { get; set; }

    public string? Equipment { get; set; }

    public string? Description { get; set; }

    public string? VideoUrl { get; set; }
}
