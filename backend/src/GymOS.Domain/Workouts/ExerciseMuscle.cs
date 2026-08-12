using GymOS.Domain.Common;

namespace GymOS.Domain.Workouts;

/// <summary>
/// How much of a movement's work a muscle group actually does.
/// </summary>
public enum MuscleRole
{
    /// <summary>
    /// The group the movement is FOR. Exactly one per exercise, and it is the group the movement is
    /// filed under everywhere a movement has to live in one place — the picker, the passport, the
    /// session's name, the mastery breakdown.
    /// </summary>
    Primary = 0,

    /// <summary>
    /// A group the movement genuinely works, but is not for. A deadlift is a leg movement that also
    /// loads the back; a bench press is a chest movement that also loads the triceps.
    ///
    /// Secondary work is REAL — it fatigues, and that is the whole reason this exists — but it is not
    /// equivalent to primary work, and nothing here pretends otherwise. See ExerciseMuscle for where
    /// the line is drawn and why.
    /// </summary>
    Secondary = 1
}

/// <summary>
/// One muscle group a movement works, and how much.
///
/// WHY THIS EXISTS. Exercise.MuscleGroup is a single free-text label, so every muscle-aware surface
/// in the app has been reasoning from the fiction that a movement trains exactly one thing. That
/// fiction is visible and wrong in one specific place: the recovery map told a member their back was
/// "fully rested — a good target for your next session" the morning after heavy deadlifts. It is the
/// same class of defect as the fabricated rep count — the app stating something about the member's
/// own body that the member can check and find untrue.
///
/// WHERE THE LINE IS DRAWN, and this is the whole design:
///
///   PRIMARY decides where a movement is FILED.  Picker category, passport region, session
///   character, muscle-group mastery. Each of these needs a movement to live in exactly one place,
///   and every one of them is arithmetic a member can check — a passport that lists a deadlift under
///   both Legs and Back makes "3 of 65" disagree with the sum of its own regions.
///
///   PRIMARY + SECONDARY together decide what a session WORKED.  Recovery, and the body map that
///   draws it. These two make a claim about the member's body rather than about the catalogue, and
///   for them the honest answer is that a deadlift did work your back.
///
/// The consequence, stated plainly: NO member's stored numbers are rewritten by this change. Sets,
/// weights, volumes, records and mastery are all keyed on the exercise and are untouched. The only
/// figures that move are recovery and the body map, which hold no stored projection — they are
/// recomputed from history on every request — so they simply start being right. Nothing is
/// backfilled onto a member, because nothing about what they did has changed; only what the gym
/// knows about the movements has.
///
/// WHY SECONDARY IS NOT WEIGHTED. The obvious alternative is to count secondary work at some
/// fraction — half a session, a third of the volume. Every candidate fraction is invented: the app
/// holds no intensity model, and a number chosen to look plausible is exactly the kind of number
/// this codebase has spent its life deleting. So secondary work counts as WORK for the question
/// "has this been trained recently", where the answer is genuinely yes, and counts for nothing at
/// all in any figure claiming to measure how much.
/// </summary>
public class ExerciseMuscle : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid ExerciseId { get; set; }

    public Exercise? Exercise { get; set; }

    /// <summary>
    /// The CANONICAL group key from <see cref="MuscleGroupVocabulary"/> — "legs", "back", never the
    /// gym's free text.
    ///
    /// Resolved once, on the way in, rather than stored raw and resolved by each reader. Exercise
    /// .MuscleGroup stays free text because a gym owner types it and should be able to; this table is
    /// the app's own reasoning, and reasoning over free text is what put a fatigued "Quads" beside a
    /// body map that could only shade "legs".
    /// </summary>
    public string MuscleGroupKey { get; set; } = string.Empty;

    public MuscleRole Role { get; set; } = MuscleRole.Secondary;
}
