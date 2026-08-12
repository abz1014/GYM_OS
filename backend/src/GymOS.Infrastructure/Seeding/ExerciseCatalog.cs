using GymOS.Domain.Workouts;

namespace GymOS.Infrastructure.Seeding;

/// <summary>
/// The exercise library a gym starts with.
///
/// This was fifteen movements, which is a demo, not a catalogue: a member opening the picker to
/// choose what they are training today found one option per muscle group and no way to say "incline
/// dumbbell press" at all. Sixty-five is the smallest list that covers what an ordinary commercial
/// gym floor can actually do — every group has enough entries that picking is a choice rather than
/// an acceptance, and every common barbell, dumbbell, cable, machine and bodyweight variant of the
/// main patterns is present.
///
/// LoadType is the field the progress rules read, and the only one here that changes behaviour.
/// MuscleGroup and Equipment are labels for people — the picker groups by
/// <see cref="MuscleGroupVocabulary"/> rather than by the raw string, so a gym renaming "Legs" to
/// "Lower body" changes only what is printed.
///
/// Getting LoadType right matters more than it looks. A run typed Weighted puts the treadmill back
/// into the overload suggestions and the member is told to add 2.5% to a jog; a plank typed Weighted
/// asks them what they planked. See <see cref="ExerciseLoadType"/>.
/// </summary>
public static class ExerciseCatalog
{
    /// <param name="MuscleGroup">The PRIMARY group — what the movement is for, and the one place it is
    /// filed: picker category, passport region, session name, mastery. Free text, because a gym owner
    /// owns this label.</param>
    /// <param name="Secondary">Canonical <see cref="MuscleGroupVocabulary"/> keys for the groups the
    /// movement also genuinely works. Empty for an isolation movement — a barbell curl works one
    /// thing, and listing a token second group to make the data look richer would be a lie the
    /// recovery map then repeats back to the member.
    ///
    /// The bar for inclusion is FATIGUE, not involvement: a muscle is listed only if training this
    /// movement hard would leave it needing recovery. Every muscle stabilises everything to some
    /// degree, and a table built on "is involved" ends up marking the whole body worked after a set
    /// of curls, which is the same uselessness as marking nothing worked.</param>
    public record CatalogEntry(
        string Name,
        string MuscleGroup,
        string Equipment,
        ExerciseLoadType LoadType,
        IReadOnlyList<string> Secondary);

    /// <summary>
    /// Every movement a new tenant is seeded with. Order is irrelevant — the picker sorts by muscle
    /// group and then by name — but it is grouped here so a human can read it.
    /// </summary>
    public static readonly IReadOnlyList<CatalogEntry> All =
    [
        // ---- Chest ----
        new("Bench Press", "Chest", "Barbell", ExerciseLoadType.Weighted, ["shoulders", "arms"]),
        new("Incline Bench Press", "Chest", "Barbell", ExerciseLoadType.Weighted, ["shoulders", "arms"]),
        new("Dumbbell Bench Press", "Chest", "Dumbbell", ExerciseLoadType.Weighted, ["shoulders", "arms"]),
        new("Incline Dumbbell Press", "Chest", "Dumbbell", ExerciseLoadType.Weighted, ["shoulders", "arms"]),
        new("Chest Fly", "Chest", "Machine", ExerciseLoadType.Weighted, ["shoulders"]),
        new("Cable Crossover", "Chest", "Cable Machine", ExerciseLoadType.Weighted, ["shoulders"]),
        new("Push-Up", "Chest", "Bodyweight", ExerciseLoadType.Bodyweight, ["shoulders", "arms", "core"]),
        new("Chest Dip", "Chest", "Bodyweight", ExerciseLoadType.Bodyweight, ["shoulders", "arms"]),

        // ---- Back ----
        new("Deadlift", "Back", "Barbell", ExerciseLoadType.Weighted, ["legs", "core"]),
        new("Bent-Over Row", "Back", "Barbell", ExerciseLoadType.Weighted, ["arms", "core"]),
        new("T-Bar Row", "Back", "Barbell", ExerciseLoadType.Weighted, ["arms", "core"]),
        new("Single-Arm Dumbbell Row", "Back", "Dumbbell", ExerciseLoadType.Weighted, ["arms", "core"]),
        new("Lat Pulldown", "Back", "Cable Machine", ExerciseLoadType.Weighted, ["arms"]),
        new("Seated Cable Row", "Back", "Cable Machine", ExerciseLoadType.Weighted, ["arms"]),
        new("Straight-Arm Pulldown", "Back", "Cable Machine", ExerciseLoadType.Weighted, ["core"]),
        new("Face Pull", "Back", "Cable Machine", ExerciseLoadType.Weighted, ["shoulders"]),
        new("Barbell Shrug", "Back", "Barbell", ExerciseLoadType.Weighted, ["shoulders"]),
        new("Pull-Up", "Back", "Bodyweight", ExerciseLoadType.Bodyweight, ["arms", "core"]),
        new("Chin-Up", "Back", "Bodyweight", ExerciseLoadType.Bodyweight, ["arms", "core"]),

        // ---- Shoulders ----
        new("Overhead Press", "Shoulders", "Barbell", ExerciseLoadType.Weighted, ["arms", "core"]),
        new("Dumbbell Shoulder Press", "Shoulders", "Dumbbell", ExerciseLoadType.Weighted, ["arms", "core"]),
        new("Arnold Press", "Shoulders", "Dumbbell", ExerciseLoadType.Weighted, ["arms"]),
        new("Lateral Raise", "Shoulders", "Dumbbell", ExerciseLoadType.Weighted, []),
        new("Cable Lateral Raise", "Shoulders", "Cable Machine", ExerciseLoadType.Weighted, []),
        new("Front Raise", "Shoulders", "Dumbbell", ExerciseLoadType.Weighted, []),
        new("Rear Delt Fly", "Shoulders", "Dumbbell", ExerciseLoadType.Weighted, ["back"]),
        new("Upright Row", "Shoulders", "Barbell", ExerciseLoadType.Weighted, ["back", "arms"]),

        // ---- Arms ----
        new("Barbell Curl", "Arms", "Barbell", ExerciseLoadType.Weighted, []),
        new("Dumbbell Curl", "Arms", "Dumbbell", ExerciseLoadType.Weighted, []),
        new("Hammer Curl", "Arms", "Dumbbell", ExerciseLoadType.Weighted, []),
        new("Preacher Curl", "Arms", "Machine", ExerciseLoadType.Weighted, []),
        new("Cable Curl", "Arms", "Cable Machine", ExerciseLoadType.Weighted, []),
        new("Tricep Pushdown", "Arms", "Cable Machine", ExerciseLoadType.Weighted, []),
        new("Overhead Tricep Extension", "Arms", "Dumbbell", ExerciseLoadType.Weighted, []),
        new("Skull Crusher", "Arms", "Barbell", ExerciseLoadType.Weighted, []),
        new("Close-Grip Bench Press", "Arms", "Barbell", ExerciseLoadType.Weighted, ["chest", "shoulders"]),
        new("Tricep Dip", "Arms", "Bodyweight", ExerciseLoadType.Bodyweight, ["chest", "shoulders"]),

        // ---- Legs ----
        new("Barbell Squat", "Legs", "Barbell", ExerciseLoadType.Weighted, ["core", "back"]),
        new("Front Squat", "Legs", "Barbell", ExerciseLoadType.Weighted, ["core", "back"]),
        new("Goblet Squat", "Legs", "Dumbbell", ExerciseLoadType.Weighted, ["core"]),
        new("Leg Press", "Legs", "Machine", ExerciseLoadType.Weighted, []),
        new("Romanian Deadlift", "Legs", "Barbell", ExerciseLoadType.Weighted, ["back", "core"]),
        new("Hip Thrust", "Legs", "Barbell", ExerciseLoadType.Weighted, ["core"]),
        new("Leg Curl", "Legs", "Machine", ExerciseLoadType.Weighted, []),
        new("Leg Extension", "Legs", "Machine", ExerciseLoadType.Weighted, []),
        new("Walking Lunge", "Legs", "Dumbbell", ExerciseLoadType.Weighted, ["core"]),
        new("Bulgarian Split Squat", "Legs", "Dumbbell", ExerciseLoadType.Weighted, ["core"]),
        new("Standing Calf Raise", "Legs", "Machine", ExerciseLoadType.Weighted, []),
        new("Seated Calf Raise", "Legs", "Machine", ExerciseLoadType.Weighted, []),

        // ---- Core ----
        // Planks are Timed and everything else here is Bodyweight, with one exception: a cable crunch
        // is done against a selectorised stack, so the load is real and worth progressing.
        new("Plank", "Core", "Bodyweight", ExerciseLoadType.Timed, ["shoulders"]),
        new("Side Plank", "Core", "Bodyweight", ExerciseLoadType.Timed, ["shoulders"]),
        new("Hanging Leg Raise", "Core", "Bodyweight", ExerciseLoadType.Bodyweight, ["arms"]),
        new("Cable Crunch", "Core", "Cable Machine", ExerciseLoadType.Weighted, []),
        new("Russian Twist", "Core", "Bodyweight", ExerciseLoadType.Bodyweight, []),
        new("Ab Wheel Rollout", "Core", "Bodyweight", ExerciseLoadType.Bodyweight, ["shoulders", "back"]),
        new("Dead Bug", "Core", "Bodyweight", ExerciseLoadType.Bodyweight, []),

        // ---- Full body ----
        new("Rowing Machine", "Full Body", "Rowing Machine", ExerciseLoadType.Distance, ["back", "legs", "arms"]),
        new("Kettlebell Swing", "Full Body", "Kettlebell", ExerciseLoadType.Weighted, ["legs", "back", "core"]),
        new("Farmer's Carry", "Full Body", "Dumbbell", ExerciseLoadType.Distance, ["back", "core", "arms"]),
        new("Burpee", "Full Body", "Bodyweight", ExerciseLoadType.Bodyweight, ["chest", "legs", "core"]),

        // ---- Cardio ----
        new("Treadmill Run", "Cardio", "Treadmill", ExerciseLoadType.Distance, ["legs"]),
        new("Stationary Bike", "Cardio", "Exercise Bike", ExerciseLoadType.Distance, ["legs"]),
        new("Elliptical Trainer", "Cardio", "Elliptical", ExerciseLoadType.Distance, ["legs"]),
        new("Stair Climber", "Cardio", "Stair Climber", ExerciseLoadType.Distance, ["legs"]),
        new("Jump Rope", "Cardio", "Jump Rope", ExerciseLoadType.Timed, ["legs"]),
    ];
}
