using GymOS.Domain.Workouts;

namespace GymOS.Infrastructure.Seeding;

public partial class DemoDataSeeder
{
    private async Task SeedExerciseLibraryAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var exercises = new (string Name, string MuscleGroup, string Equipment)[]
        {
            ("Barbell Squat", "Legs", "Barbell"),
            ("Bench Press", "Chest", "Barbell"),
            ("Deadlift", "Back", "Barbell"),
            ("Overhead Press", "Shoulders", "Barbell"),
            ("Bent-Over Row", "Back", "Barbell"),
            ("Pull-Up", "Back", "Bodyweight"),
            ("Push-Up", "Chest", "Bodyweight"),
            ("Dumbbell Curl", "Arms", "Dumbbell"),
            ("Tricep Pushdown", "Arms", "Cable Machine"),
            ("Lat Pulldown", "Back", "Cable Machine"),
            ("Leg Press", "Legs", "Machine"),
            ("Leg Curl", "Legs", "Machine"),
            ("Plank", "Core", "Bodyweight"),
            ("Treadmill Run", "Cardio", "Treadmill"),
            ("Rowing Machine", "Full Body", "Rowing Machine"),
        };

        foreach (var (name, muscleGroup, equipment) in exercises)
        {
            db.Exercises.Add(new Exercise { TenantId = tenantId, Name = name, MuscleGroup = muscleGroup, Equipment = equipment });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
