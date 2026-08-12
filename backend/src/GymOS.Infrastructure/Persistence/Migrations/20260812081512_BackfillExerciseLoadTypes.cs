using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymOS.Infrastructure.Persistence.Migrations;

/// <summary>
/// Types the exercises that already exist, and deletes the kilograms that were never real.
///
/// AddExerciseLoadType defaulted every existing row to Weighted. That is the right default for a
/// column being added to a catalogue of barbells and machines, and it is wrong for the two treadmill
/// entries in it — and the seeder that types them correctly only runs against an empty database, so
/// a deployment that already had a tenant kept telling members to add 2.5% to a run. Confirmed on
/// production after the previous migration shipped: the member home screen still read "Treadmill Run
/// came down last session".
///
/// Matching by name is deliberately narrow. These are the fifteen movements DemoDataSeeder creates,
/// so this corrects exactly the rows this project put there and silently does nothing to anything a
/// real gym typed itself — which is the right trade: mistyping a customer's own catalogue from a
/// migration would be worse than leaving it Weighted, and the exercise editor is where that belongs.
///
/// The weight deletion is not cleanup for its own sake. WorkoutLogEntry rows for these movements
/// carry loads the seeder invented — "Treadmill Run, 4x9, 17.5 kg" — because it decided load by
/// string-matching Equipment against "Bodyweight" and a treadmill is not equipped with bodyweight.
/// Filtering the suggestions stops the app SAYING anything false about them; this removes the false
/// number itself, so nothing computed later can find it again. Down() restores the types and cannot
/// restore the weights, which is correct: they were never a measurement of anything.
/// </summary>
public partial class BackfillExerciseLoadTypes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE "Exercises" SET "LoadType" = 'Bodyweight'
             WHERE "Name" IN ('Pull-Up', 'Push-Up') AND "LoadType" = 'Weighted';

            UPDATE "Exercises" SET "LoadType" = 'Timed'
             WHERE "Name" = 'Plank' AND "LoadType" = 'Weighted';

            UPDATE "Exercises" SET "LoadType" = 'Distance'
             WHERE "Name" IN ('Treadmill Run', 'Rowing Machine') AND "LoadType" = 'Weighted';
            """);

        // Every logged set of a movement that carries no external load. Scoped by LoadType rather
        // than by name so it also cleans anything a gym has since typed as unloaded itself.
        migrationBuilder.Sql("""
            UPDATE "WorkoutLogEntries" e
               SET "WeightKg" = NULL
              FROM "Exercises" x
             WHERE x."Id" = e."ExerciseId"
               AND x."LoadType" <> 'Weighted'
               AND e."WeightKg" IS NOT NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE "Exercises" SET "LoadType" = 'Weighted'
             WHERE "Name" IN ('Pull-Up', 'Push-Up', 'Plank', 'Treadmill Run', 'Rowing Machine');
            """);
    }
}
