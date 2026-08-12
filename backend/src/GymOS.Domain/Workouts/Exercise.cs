using GymOS.Domain.Common;

namespace GymOS.Domain.Workouts;

public class Exercise : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The PRIMARY group, as the gym typed it. Still free text, still a single value, and still the
    /// one place a movement is filed — see <see cref="ExerciseMuscle"/> for why that did not change
    /// and what <see cref="Muscles"/> adds beside it.
    /// </summary>
    public string? MuscleGroup { get; set; }

    /// <summary>
    /// Every group this movement works, primary and secondary, in canonical vocabulary keys.
    ///
    /// Additive: <see cref="MuscleGroup"/> above is untouched and every existing reader still gets
    /// the same answer it always did. Only the two surfaces that make a claim about the member's
    /// BODY — recovery and the body map — read this instead.
    /// </summary>
    public ICollection<ExerciseMuscle> Muscles { get; set; } = new List<ExerciseMuscle>();

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
