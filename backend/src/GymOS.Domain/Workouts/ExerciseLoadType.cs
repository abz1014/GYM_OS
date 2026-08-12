namespace GymOS.Domain.Workouts;

/// <summary>
/// How a movement is loaded, and therefore what "getting better at it" means.
///
/// Nothing in this system could tell a treadmill from a bench press. Exercise carried Name,
/// MuscleGroup, Equipment and Description — all nullable free text — so every rule that reasons
/// about progress reasoned in kilograms, because kilograms were the only thing it had. The member
/// home screen consequently told people their treadmill run had stalled and offered them 2.5% more
/// weight on it, and the seeder cheerfully wrote 17.5 kg against a run because it decided load by
/// string-matching Equipment against the literal "Bodyweight".
///
/// A typed discriminator rather than a convention on MuscleGroup: a real gym renaming its "Cardio"
/// group to "Conditioning" must not silently re-enable a nonsense suggestion, and a value a query
/// can filter on is checkable in a way a string comparison never is.
///
/// This says how a movement is MEASURED, not which muscles it uses — a plank and a treadmill run are
/// both cardio to a member and are not the same measurement problem.
/// </summary>
public enum ExerciseLoadType
{
    /// <summary>
    /// External load in kilograms, progressed by adding weight or reps. The only kind
    /// ProgressiveOverloadPolicy can speak about, because it compares MaxWeightKg and TotalReps.
    /// Default for anything unspecified, which matches the overwhelming majority of a gym's catalogue
    /// and keeps existing rows behaving as they always did.
    /// </summary>
    Weighted,

    /// <summary>
    /// The member's own body — pull-ups, push-ups, dips. Reps are the progression and weight is
    /// legitimately absent, so a null WeightKg here is a fact rather than a gap. Volume computed as
    /// sets x reps x (weight ?? 0) reads zero for these, which understates the work; that is a known
    /// consequence recorded here rather than fixed by this type.
    /// </summary>
    Bodyweight,

    /// <summary>
    /// Measured in time held or worked — planks, wall sits, stretches. Reps are meaningless and the
    /// schema has nowhere to put the duration yet, so nothing may claim progress on these.
    /// </summary>
    Timed,

    /// <summary>
    /// Measured in distance and pace — runs, rows, rides. WorkoutLogEntry has no distance, duration
    /// or pace column, so these movements are currently RECORDABLE but not MEASURABLE: a member can
    /// name that they did one, and the system can say nothing true about whether it went well.
    /// Excluding them from progress rules is the honest interim state, not the finished feature.
    /// </summary>
    Distance
}
