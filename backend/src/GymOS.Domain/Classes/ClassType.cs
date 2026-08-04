using GymOS.Domain.Common;

namespace GymOS.Domain.Classes;

/// <summary>
/// A kind of group class the gym offers (Spin, Yoga, HIIT). The catalog entry — carries the
/// defaults a concrete schedule inherits (duration, capacity) so staff don't re-type them per slot.
/// Tenant-scoped rather than branch-scoped: a class type like "Spin" is a gym-wide concept; where
/// and when it runs is the branch-scoped ClassSchedule's job.
/// </summary>
public class ClassType : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int DefaultDurationMinutes { get; set; } = 45;

    public int DefaultCapacity { get; set; } = 20;

    /// <summary>Optional hex colour (e.g. "#3b82f6") used to tint this class type on the calendar.</summary>
    public string? ColorHex { get; set; }

    public bool IsActive { get; set; } = true;
}
