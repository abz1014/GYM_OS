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
    public record CatalogEntry(string Name, string MuscleGroup, string Equipment, ExerciseLoadType LoadType);

    /// <summary>
    /// Every movement a new tenant is seeded with. Order is irrelevant — the picker sorts by muscle
    /// group and then by name — but it is grouped here so a human can read it.
    /// </summary>
    public static readonly IReadOnlyList<CatalogEntry> All =
    [
        // ---- Chest ----
        new("Bench Press", "Chest", "Barbell", ExerciseLoadType.Weighted),
        new("Incline Bench Press", "Chest", "Barbell", ExerciseLoadType.Weighted),
        new("Dumbbell Bench Press", "Chest", "Dumbbell", ExerciseLoadType.Weighted),
        new("Incline Dumbbell Press", "Chest", "Dumbbell", ExerciseLoadType.Weighted),
        new("Chest Fly", "Chest", "Machine", ExerciseLoadType.Weighted),
        new("Cable Crossover", "Chest", "Cable Machine", ExerciseLoadType.Weighted),
        new("Push-Up", "Chest", "Bodyweight", ExerciseLoadType.Bodyweight),
        new("Chest Dip", "Chest", "Bodyweight", ExerciseLoadType.Bodyweight),

        // ---- Back ----
        new("Deadlift", "Back", "Barbell", ExerciseLoadType.Weighted),
        new("Bent-Over Row", "Back", "Barbell", ExerciseLoadType.Weighted),
        new("T-Bar Row", "Back", "Barbell", ExerciseLoadType.Weighted),
        new("Single-Arm Dumbbell Row", "Back", "Dumbbell", ExerciseLoadType.Weighted),
        new("Lat Pulldown", "Back", "Cable Machine", ExerciseLoadType.Weighted),
        new("Seated Cable Row", "Back", "Cable Machine", ExerciseLoadType.Weighted),
        new("Straight-Arm Pulldown", "Back", "Cable Machine", ExerciseLoadType.Weighted),
        new("Face Pull", "Back", "Cable Machine", ExerciseLoadType.Weighted),
        new("Barbell Shrug", "Back", "Barbell", ExerciseLoadType.Weighted),
        new("Pull-Up", "Back", "Bodyweight", ExerciseLoadType.Bodyweight),
        new("Chin-Up", "Back", "Bodyweight", ExerciseLoadType.Bodyweight),

        // ---- Shoulders ----
        new("Overhead Press", "Shoulders", "Barbell", ExerciseLoadType.Weighted),
        new("Dumbbell Shoulder Press", "Shoulders", "Dumbbell", ExerciseLoadType.Weighted),
        new("Arnold Press", "Shoulders", "Dumbbell", ExerciseLoadType.Weighted),
        new("Lateral Raise", "Shoulders", "Dumbbell", ExerciseLoadType.Weighted),
        new("Cable Lateral Raise", "Shoulders", "Cable Machine", ExerciseLoadType.Weighted),
        new("Front Raise", "Shoulders", "Dumbbell", ExerciseLoadType.Weighted),
        new("Rear Delt Fly", "Shoulders", "Dumbbell", ExerciseLoadType.Weighted),
        new("Upright Row", "Shoulders", "Barbell", ExerciseLoadType.Weighted),

        // ---- Arms ----
        new("Barbell Curl", "Arms", "Barbell", ExerciseLoadType.Weighted),
        new("Dumbbell Curl", "Arms", "Dumbbell", ExerciseLoadType.Weighted),
        new("Hammer Curl", "Arms", "Dumbbell", ExerciseLoadType.Weighted),
        new("Preacher Curl", "Arms", "Machine", ExerciseLoadType.Weighted),
        new("Cable Curl", "Arms", "Cable Machine", ExerciseLoadType.Weighted),
        new("Tricep Pushdown", "Arms", "Cable Machine", ExerciseLoadType.Weighted),
        new("Overhead Tricep Extension", "Arms", "Dumbbell", ExerciseLoadType.Weighted),
        new("Skull Crusher", "Arms", "Barbell", ExerciseLoadType.Weighted),
        new("Close-Grip Bench Press", "Arms", "Barbell", ExerciseLoadType.Weighted),
        new("Tricep Dip", "Arms", "Bodyweight", ExerciseLoadType.Bodyweight),

        // ---- Legs ----
        new("Barbell Squat", "Legs", "Barbell", ExerciseLoadType.Weighted),
        new("Front Squat", "Legs", "Barbell", ExerciseLoadType.Weighted),
        new("Goblet Squat", "Legs", "Dumbbell", ExerciseLoadType.Weighted),
        new("Leg Press", "Legs", "Machine", ExerciseLoadType.Weighted),
        new("Romanian Deadlift", "Legs", "Barbell", ExerciseLoadType.Weighted),
        new("Hip Thrust", "Legs", "Barbell", ExerciseLoadType.Weighted),
        new("Leg Curl", "Legs", "Machine", ExerciseLoadType.Weighted),
        new("Leg Extension", "Legs", "Machine", ExerciseLoadType.Weighted),
        new("Walking Lunge", "Legs", "Dumbbell", ExerciseLoadType.Weighted),
        new("Bulgarian Split Squat", "Legs", "Dumbbell", ExerciseLoadType.Weighted),
        new("Standing Calf Raise", "Legs", "Machine", ExerciseLoadType.Weighted),
        new("Seated Calf Raise", "Legs", "Machine", ExerciseLoadType.Weighted),

        // ---- Core ----
        // Planks are Timed and everything else here is Bodyweight, with one exception: a cable crunch
        // is done against a selectorised stack, so the load is real and worth progressing.
        new("Plank", "Core", "Bodyweight", ExerciseLoadType.Timed),
        new("Side Plank", "Core", "Bodyweight", ExerciseLoadType.Timed),
        new("Hanging Leg Raise", "Core", "Bodyweight", ExerciseLoadType.Bodyweight),
        new("Cable Crunch", "Core", "Cable Machine", ExerciseLoadType.Weighted),
        new("Russian Twist", "Core", "Bodyweight", ExerciseLoadType.Bodyweight),
        new("Ab Wheel Rollout", "Core", "Bodyweight", ExerciseLoadType.Bodyweight),
        new("Dead Bug", "Core", "Bodyweight", ExerciseLoadType.Bodyweight),

        // ---- Full body ----
        new("Rowing Machine", "Full Body", "Rowing Machine", ExerciseLoadType.Distance),
        new("Kettlebell Swing", "Full Body", "Kettlebell", ExerciseLoadType.Weighted),
        new("Farmer's Carry", "Full Body", "Dumbbell", ExerciseLoadType.Distance),
        new("Burpee", "Full Body", "Bodyweight", ExerciseLoadType.Bodyweight),

        // ---- Cardio ----
        new("Treadmill Run", "Cardio", "Treadmill", ExerciseLoadType.Distance),
        new("Stationary Bike", "Cardio", "Exercise Bike", ExerciseLoadType.Distance),
        new("Elliptical Trainer", "Cardio", "Elliptical", ExerciseLoadType.Distance),
        new("Stair Climber", "Cardio", "Stair Climber", ExerciseLoadType.Distance),
        new("Jump Rope", "Cardio", "Jump Rope", ExerciseLoadType.Timed),
    ];
}
