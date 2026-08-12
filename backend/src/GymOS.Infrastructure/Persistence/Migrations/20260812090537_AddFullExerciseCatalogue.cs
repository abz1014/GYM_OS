using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymOS.Infrastructure.Persistence.Migrations;

/// <summary>
/// Gives every existing gym the full exercise catalogue, not just the fifteen the demo shipped with.
///
/// DemoDataSeeder only runs against an empty database, so an instance that already has a tenant — the
/// deployed one, and any real gym — would never see a catalogue expansion. That matters more than
/// usual here because the next screen built on top of this is a picker whose entire job is to let a
/// member choose what they are training today: with fifteen movements there is one option per muscle
/// group and choosing is a formality.
///
/// The VALUES list is a deliberate frozen snapshot of <c>ExerciseCatalog.All</c> as of this migration,
/// not a read of it. A migration that computed its own contents from a class would change meaning
/// every time that class changed, and would silently re-run differently on a database that had
/// already applied it. When the catalogue grows again, that is a new migration.
///
/// Matching is by (TenantId, Name), so the fifteen already present are left exactly as they are —
/// including their Ids, which WorkoutLogEntry, WorkoutTemplateExercise and SkillNode all reference.
/// Nothing here updates an existing row: a gym that has renamed or re-typed one of them keeps their
/// version. Re-running is a no-op.
///
/// Down() removes only rows nothing points at. An exercise a member has logged, a trainer has put in
/// a template, or a skill tree names is left in place — reversing a catalogue addition is not worth
/// destroying a member's history, and the FK would refuse anyway.
/// </summary>
public partial class AddFullExerciseCatalogue : Migration
{
    /// <summary>The catalogue as of 2026-08-12. Frozen — see the class comment.</summary>
    private const string CatalogueValues = """
        ('Bench Press', 'Chest', 'Barbell', 'Weighted'),
        ('Incline Bench Press', 'Chest', 'Barbell', 'Weighted'),
        ('Dumbbell Bench Press', 'Chest', 'Dumbbell', 'Weighted'),
        ('Incline Dumbbell Press', 'Chest', 'Dumbbell', 'Weighted'),
        ('Chest Fly', 'Chest', 'Machine', 'Weighted'),
        ('Cable Crossover', 'Chest', 'Cable Machine', 'Weighted'),
        ('Push-Up', 'Chest', 'Bodyweight', 'Bodyweight'),
        ('Chest Dip', 'Chest', 'Bodyweight', 'Bodyweight'),
        ('Deadlift', 'Back', 'Barbell', 'Weighted'),
        ('Bent-Over Row', 'Back', 'Barbell', 'Weighted'),
        ('T-Bar Row', 'Back', 'Barbell', 'Weighted'),
        ('Single-Arm Dumbbell Row', 'Back', 'Dumbbell', 'Weighted'),
        ('Lat Pulldown', 'Back', 'Cable Machine', 'Weighted'),
        ('Seated Cable Row', 'Back', 'Cable Machine', 'Weighted'),
        ('Straight-Arm Pulldown', 'Back', 'Cable Machine', 'Weighted'),
        ('Face Pull', 'Back', 'Cable Machine', 'Weighted'),
        ('Barbell Shrug', 'Back', 'Barbell', 'Weighted'),
        ('Pull-Up', 'Back', 'Bodyweight', 'Bodyweight'),
        ('Chin-Up', 'Back', 'Bodyweight', 'Bodyweight'),
        ('Overhead Press', 'Shoulders', 'Barbell', 'Weighted'),
        ('Dumbbell Shoulder Press', 'Shoulders', 'Dumbbell', 'Weighted'),
        ('Arnold Press', 'Shoulders', 'Dumbbell', 'Weighted'),
        ('Lateral Raise', 'Shoulders', 'Dumbbell', 'Weighted'),
        ('Cable Lateral Raise', 'Shoulders', 'Cable Machine', 'Weighted'),
        ('Front Raise', 'Shoulders', 'Dumbbell', 'Weighted'),
        ('Rear Delt Fly', 'Shoulders', 'Dumbbell', 'Weighted'),
        ('Upright Row', 'Shoulders', 'Barbell', 'Weighted'),
        ('Barbell Curl', 'Arms', 'Barbell', 'Weighted'),
        ('Dumbbell Curl', 'Arms', 'Dumbbell', 'Weighted'),
        ('Hammer Curl', 'Arms', 'Dumbbell', 'Weighted'),
        ('Preacher Curl', 'Arms', 'Machine', 'Weighted'),
        ('Cable Curl', 'Arms', 'Cable Machine', 'Weighted'),
        ('Tricep Pushdown', 'Arms', 'Cable Machine', 'Weighted'),
        ('Overhead Tricep Extension', 'Arms', 'Dumbbell', 'Weighted'),
        ('Skull Crusher', 'Arms', 'Barbell', 'Weighted'),
        ('Close-Grip Bench Press', 'Arms', 'Barbell', 'Weighted'),
        ('Tricep Dip', 'Arms', 'Bodyweight', 'Bodyweight'),
        ('Barbell Squat', 'Legs', 'Barbell', 'Weighted'),
        ('Front Squat', 'Legs', 'Barbell', 'Weighted'),
        ('Goblet Squat', 'Legs', 'Dumbbell', 'Weighted'),
        ('Leg Press', 'Legs', 'Machine', 'Weighted'),
        ('Romanian Deadlift', 'Legs', 'Barbell', 'Weighted'),
        ('Hip Thrust', 'Legs', 'Barbell', 'Weighted'),
        ('Leg Curl', 'Legs', 'Machine', 'Weighted'),
        ('Leg Extension', 'Legs', 'Machine', 'Weighted'),
        ('Walking Lunge', 'Legs', 'Dumbbell', 'Weighted'),
        ('Bulgarian Split Squat', 'Legs', 'Dumbbell', 'Weighted'),
        ('Standing Calf Raise', 'Legs', 'Machine', 'Weighted'),
        ('Seated Calf Raise', 'Legs', 'Machine', 'Weighted'),
        ('Plank', 'Core', 'Bodyweight', 'Timed'),
        ('Side Plank', 'Core', 'Bodyweight', 'Timed'),
        ('Hanging Leg Raise', 'Core', 'Bodyweight', 'Bodyweight'),
        ('Cable Crunch', 'Core', 'Cable Machine', 'Weighted'),
        ('Russian Twist', 'Core', 'Bodyweight', 'Bodyweight'),
        ('Ab Wheel Rollout', 'Core', 'Bodyweight', 'Bodyweight'),
        ('Dead Bug', 'Core', 'Bodyweight', 'Bodyweight'),
        ('Rowing Machine', 'Full Body', 'Rowing Machine', 'Distance'),
        ('Kettlebell Swing', 'Full Body', 'Kettlebell', 'Weighted'),
        ('Farmer''s Carry', 'Full Body', 'Dumbbell', 'Distance'),
        ('Burpee', 'Full Body', 'Bodyweight', 'Bodyweight'),
        ('Treadmill Run', 'Cardio', 'Treadmill', 'Distance'),
        ('Stationary Bike', 'Cardio', 'Exercise Bike', 'Distance'),
        ('Elliptical Trainer', 'Cardio', 'Elliptical', 'Distance'),
        ('Stair Climber', 'Cardio', 'Stair Climber', 'Distance'),
        ('Jump Rope', 'Cardio', 'Jump Rope', 'Timed')
        """;

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // gen_random_uuid() is built into PostgreSQL 13+; this project targets 16.
        migrationBuilder.Sql($"""
            INSERT INTO "Exercises" ("Id", "TenantId", "Name", "MuscleGroup", "Equipment", "LoadType")
            SELECT gen_random_uuid(), t."Id", c.name, c.muscle_group, c.equipment, c.load_type
              FROM "Tenants" t
             CROSS JOIN (VALUES
            {CatalogueValues}
             ) AS c(name, muscle_group, equipment, load_type)
             WHERE NOT EXISTS (
                   SELECT 1 FROM "Exercises" e
                    WHERE e."TenantId" = t."Id" AND e."Name" = c.name);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($"""
            DELETE FROM "Exercises" e
             USING (VALUES
            {CatalogueValues}
             ) AS c(name, muscle_group, equipment, load_type)
             WHERE e."Name" = c.name
               AND NOT EXISTS (SELECT 1 FROM "WorkoutLogEntries" w WHERE w."ExerciseId" = e."Id")
               AND NOT EXISTS (SELECT 1 FROM "WorkoutTemplateExercises" w WHERE w."ExerciseId" = e."Id")
               AND NOT EXISTS (SELECT 1 FROM "SkillNodes" s WHERE s."ExerciseId" = e."Id")
               AND NOT EXISTS (SELECT 1 FROM "PersonalRecords" p WHERE p."ExerciseId" = e."Id")
               AND NOT EXISTS (SELECT 1 FROM "ExerciseMasteries" m WHERE m."ExerciseId" = e."Id");
            """);
    }
}
