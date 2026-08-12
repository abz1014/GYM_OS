using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymOS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Gives every movement the full set of muscle groups it works, so the recovery map can stop
    /// telling members a muscle is rested the morning after they trained it.
    ///
    /// THE DEFECT. Exercise.MuscleGroup is one label, so a deadlift was "Back" and nothing else. The
    /// day after heavy deadlifts the Train screen shaded legs as untouched and the recovery list said
    /// "Fully rested - a good target for your next session". That is the app making a claim about the
    /// member's body which their body contradicts, and unlike a wrong number it is actively harmful:
    /// acting on it means loading a fatigued muscle.
    ///
    /// WHAT THIS DOES NOT DO, which is the important half. No member row is touched. Sets, weights,
    /// volumes, personal records, mastery percentages and passport coverage are all keyed on the
    /// exercise and are left exactly as they were. Nothing about what anyone DID has changed - only
    /// what the catalogue knows about the movements - so there is nothing to recompute and no member
    /// wakes up with a different number against their name. Recovery and the body map change because
    /// they hold no stored projection at all: both are recomputed from history on every request, so
    /// they simply start being right.
    ///
    /// PRIMARY IS DERIVED, NEVER LISTED. Every exercise gets a Primary row resolved from its own
    /// MuscleGroup label through the same vocabulary the app uses. Listing primaries as literals
    /// would let this table disagree with a label the gym can still edit, which would file a movement
    /// under one group in the picker and shade a different one on the map.
    ///
    /// SECONDARIES ARE FROZEN LITERALS, matched by exercise NAME. They are the anatomy of the
    /// movements this project ships in ExerciseCatalog, copied here rather than computed from it,
    /// because a migration is a snapshot: if that class gains a movement next month, this migration
    /// must still do in a year what it did the day it ran. A gym's own custom movements match nothing
    /// and get a primary row only - correct, because nobody here knows what a gym's "Sled Push"
    /// works, and guessing would be the invention this whole change exists to remove.
    ///
    /// Idempotent throughout (NOT EXISTS guards), because the seeder writes the same rows for a fresh
    /// database and the two must be able to run over each other without doubling a group's weight.
    /// </summary>
    public partial class AddExerciseMuscles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExerciseMuscles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                    MuscleGroupKey = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExerciseMuscles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExerciseMuscles_Exercises_ExerciseId",
                        column: x => x.ExerciseId,
                        principalTable: "Exercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseMuscles_ExerciseId_MuscleGroupKey",
                table: "ExerciseMuscles",
                columns: new[] { "ExerciseId", "MuscleGroupKey" },
                unique: true);

            // ---- 1. One Primary row per exercise, resolved from the gym's own label ----
            //
            // The synonym table mirrors MuscleGroupVocabulary as it stood when this was written. An
            // unrecognised or missing label resolves to 'other', exactly as the C# does - a real,
            // visible group rather than a movement that silently drops off the map.
            migrationBuilder.Sql("""
                WITH synonyms(label, key) AS (VALUES
                    ('chest', 'chest'),
                    ('pecs', 'chest'),
                    ('pectorals', 'chest'),
                    ('back', 'back'),
                    ('lats', 'back'),
                    ('latissimus', 'back'),
                    ('traps', 'back'),
                    ('trapezius', 'back'),
                    ('rhomboids', 'back'),
                    ('upper back', 'back'),
                    ('lower back', 'back'),
                    ('shoulders', 'shoulders'),
                    ('shoulder', 'shoulders'),
                    ('delts', 'shoulders'),
                    ('deltoids', 'shoulders'),
                    ('arms', 'arms'),
                    ('arm', 'arms'),
                    ('biceps', 'arms'),
                    ('triceps', 'arms'),
                    ('forearms', 'arms'),
                    ('legs', 'legs'),
                    ('leg', 'legs'),
                    ('quads', 'legs'),
                    ('quadriceps', 'legs'),
                    ('hamstrings', 'legs'),
                    ('glutes', 'legs'),
                    ('calves', 'legs'),
                    ('lower body', 'legs'),
                    ('core', 'core'),
                    ('abs', 'core'),
                    ('abdominals', 'core'),
                    ('obliques', 'core'),
                    ('full body', 'fullbody'),
                    ('fullbody', 'fullbody'),
                    ('total body', 'fullbody'),
                    ('compound', 'fullbody'),
                    ('cardio', 'cardio'),
                    ('conditioning', 'cardio'),
                    ('endurance', 'cardio')
                )
                INSERT INTO "ExerciseMuscles" ("Id", "TenantId", "ExerciseId", "MuscleGroupKey", "Role")
                SELECT gen_random_uuid(), e."TenantId", e."Id",
                       COALESCE(s.key, 'other'), 'Primary'
                  FROM "Exercises" e
                  LEFT JOIN synonyms s ON s.label = lower(trim(e."MuscleGroup"))
                 WHERE NOT EXISTS (
                       SELECT 1 FROM "ExerciseMuscles" m
                        WHERE m."ExerciseId" = e."Id" AND m."Role" = 'Primary');
                """);

            // ---- 2. Secondary rows for the movements this project ships ----
            //
            // Matched on name. The NOT EXISTS guard also stops a secondary being written where the
            // primary already claims that group - a Full Body rowing machine must not be both.
            migrationBuilder.Sql("""
                WITH anatomy(exercise_name, key) AS (VALUES
                    ('Ab Wheel Rollout', 'shoulders'),
                    ('Ab Wheel Rollout', 'back'),
                    ('Arnold Press', 'arms'),
                    ('Barbell Shrug', 'shoulders'),
                    ('Barbell Squat', 'core'),
                    ('Barbell Squat', 'back'),
                    ('Bench Press', 'shoulders'),
                    ('Bench Press', 'arms'),
                    ('Bent-Over Row', 'arms'),
                    ('Bent-Over Row', 'core'),
                    ('Bulgarian Split Squat', 'core'),
                    ('Burpee', 'chest'),
                    ('Burpee', 'legs'),
                    ('Burpee', 'core'),
                    ('Cable Crossover', 'shoulders'),
                    ('Chest Dip', 'shoulders'),
                    ('Chest Dip', 'arms'),
                    ('Chest Fly', 'shoulders'),
                    ('Chin-Up', 'arms'),
                    ('Chin-Up', 'core'),
                    ('Close-Grip Bench Press', 'chest'),
                    ('Close-Grip Bench Press', 'shoulders'),
                    ('Deadlift', 'legs'),
                    ('Deadlift', 'core'),
                    ('Dumbbell Bench Press', 'shoulders'),
                    ('Dumbbell Bench Press', 'arms'),
                    ('Dumbbell Shoulder Press', 'arms'),
                    ('Dumbbell Shoulder Press', 'core'),
                    ('Elliptical Trainer', 'legs'),
                    ('Face Pull', 'shoulders'),
                    ('Farmer''s Carry', 'back'),
                    ('Farmer''s Carry', 'core'),
                    ('Farmer''s Carry', 'arms'),
                    ('Front Squat', 'core'),
                    ('Front Squat', 'back'),
                    ('Goblet Squat', 'core'),
                    ('Hanging Leg Raise', 'arms'),
                    ('Hip Thrust', 'core'),
                    ('Incline Bench Press', 'shoulders'),
                    ('Incline Bench Press', 'arms'),
                    ('Incline Dumbbell Press', 'shoulders'),
                    ('Incline Dumbbell Press', 'arms'),
                    ('Jump Rope', 'legs'),
                    ('Kettlebell Swing', 'legs'),
                    ('Kettlebell Swing', 'back'),
                    ('Kettlebell Swing', 'core'),
                    ('Lat Pulldown', 'arms'),
                    ('Overhead Press', 'arms'),
                    ('Overhead Press', 'core'),
                    ('Plank', 'shoulders'),
                    ('Pull-Up', 'arms'),
                    ('Pull-Up', 'core'),
                    ('Push-Up', 'shoulders'),
                    ('Push-Up', 'arms'),
                    ('Push-Up', 'core'),
                    ('Rear Delt Fly', 'back'),
                    ('Romanian Deadlift', 'back'),
                    ('Romanian Deadlift', 'core'),
                    ('Rowing Machine', 'back'),
                    ('Rowing Machine', 'legs'),
                    ('Rowing Machine', 'arms'),
                    ('Seated Cable Row', 'arms'),
                    ('Side Plank', 'shoulders'),
                    ('Single-Arm Dumbbell Row', 'arms'),
                    ('Single-Arm Dumbbell Row', 'core'),
                    ('Stair Climber', 'legs'),
                    ('Stationary Bike', 'legs'),
                    ('Straight-Arm Pulldown', 'core'),
                    ('T-Bar Row', 'arms'),
                    ('T-Bar Row', 'core'),
                    ('Treadmill Run', 'legs'),
                    ('Tricep Dip', 'chest'),
                    ('Tricep Dip', 'shoulders'),
                    ('Upright Row', 'back'),
                    ('Upright Row', 'arms'),
                    ('Walking Lunge', 'core')
                )
                INSERT INTO "ExerciseMuscles" ("Id", "TenantId", "ExerciseId", "MuscleGroupKey", "Role")
                SELECT gen_random_uuid(), e."TenantId", e."Id", a.key, 'Secondary'
                  FROM "Exercises" e
                  JOIN anatomy a ON a.exercise_name = e."Name"
                 WHERE NOT EXISTS (
                       SELECT 1 FROM "ExerciseMuscles" m
                        WHERE m."ExerciseId" = e."Id" AND m."MuscleGroupKey" = a.key);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Clean: this table holds no member data, so dropping it loses nothing anybody recorded.
            // GetMyRecoveryQuery keeps its fallback to Exercise.MuscleGroup for catalogues that were
            // never backfilled, so a rollback returns the old behaviour rather than an empty map.
            migrationBuilder.DropTable(
                name: "ExerciseMuscles");
        }
    }
}
