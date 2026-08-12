using GymOS.Domain.Common;

namespace GymOS.Domain.Workouts;

public class Exercise : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? MuscleGroup { get; set; }

    public string? Equipment { get; set; }

    /// <summary>
    /// How this movement is measured — see <see cref="ExerciseLoadType"/>. Weighted by default, so
    /// existing rows and anything created without an opinion keep behaving exactly as before.
    ///
    /// MuscleGroup and Equipment are free text a gym owner types; this is the one thing about an
    /// exercise the rules are allowed to reason from.
    /// </summary>
    public ExerciseLoadType LoadType { get; set; } = ExerciseLoadType.Weighted;

    public string? Description { get; set; }

    public string? VideoUrl { get; set; }
}
