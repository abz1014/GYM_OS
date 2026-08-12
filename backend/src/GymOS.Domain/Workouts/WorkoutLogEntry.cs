using GymOS.Domain.Common;

namespace GymOS.Domain.Workouts;

public class WorkoutLogEntry : BaseEntity, ITenantScoped
{
    /// <summary>
    /// Direct tenant scoping, so isolation is a property of the schema rather than of every query
    /// that happens to start from Member.
    ///
    /// This table was reachable only through a tenant-scoped Member, which made it safe in practice
    /// and unguarded in principle: one future query beginning here instead of at Member would cross
    /// tenants silently, with nothing failing. Same class of gap as the cross-branch IDOR, same fix —
    /// enforce it in the model so nobody has to remember.
    /// </summary>
    public Guid TenantId { get; set; }

    public Guid WorkoutLogId { get; set; }

    public WorkoutLog? WorkoutLog { get; set; }

    public Guid ExerciseId { get; set; }

    /// <summary>The movement this entry records.</summary>
    public Exercise? Exercise { get; set; }

    /// <summary>
    /// How many times the movement was performed. Real for every load type — a run has one "set",
    /// a plank held three times has three — so this stays non-nullable.
    /// </summary>
    public int SetsCompleted { get; set; }

    /// <summary>
    /// Repetitions, and NULL where the movement has none.
    ///
    /// This was non-nullable, which sounds harmless and was the whole defect: a treadmill run has no
    /// rep count, so the app invented one. The picker's DEFAULT_REPS of 8 went in unchallenged, and
    /// because <see cref="ExerciseLoadType"/> was consulted nowhere on the write path, "8 reps of
    /// running" became a stored fact. It then propagated — the next-session proposal re-served it and
    /// the picker showed it back as "3 × 8 · 4d ago", so after one session the fabrication was
    /// indistinguishable from the member's own history.
    ///
    /// Nullable rather than zero. A zero is a measurement of nothing; a null is the absence of a
    /// measurement, and those are different claims. Every reader now has to decide what an absent rep
    /// count means for it, which is exactly the decision that was being skipped.
    /// </summary>
    public int? RepsCompleted { get; set; }

    /// <summary>
    /// Load. Null for anything not lifted — see the migration that deleted the invented kilograms
    /// from treadmill entries.
    ///
    /// Deliberately NOT forbidden on a Distance movement. A farmer's carry is measured in distance
    /// AND load, and the seeded catalogue contains one; a weighted-vest run is the same shape. The
    /// load type says what the PRIMARY measurement is, not what is the only permissible one.
    /// </summary>
    public decimal? WeightKg { get; set; }

    /// <summary>
    /// Seconds held or worked. The measurement Timed movements have and reps are not.
    ///
    /// Also valid on Distance: a run has both a distance and a duration, and pace is derived from
    /// the pair at read time rather than stored, so it can never disagree with them.
    /// </summary>
    public int? DurationSeconds { get; set; }

    /// <summary>Metres covered. Metres rather than kilometres so a 400m interval is not "0.4".</summary>
    public decimal? DistanceMeters { get; set; }
}
