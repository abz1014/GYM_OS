using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymOS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Gives runs and planks somewhere to put their measurement, and deletes the rep counts nobody
    /// entered.
    ///
    /// ExerciseLoadType has documented this hole in its own XML since it was written: Timed says
    /// "Reps are meaningless and the schema has nowhere to put the duration yet"; Distance says these
    /// movements are "RECORDABLE but not MEASURABLE". The picker filled the gap with a constant —
    /// DEFAULT_REPS = 8 — applied to every movement without consulting the load type it already had,
    /// so "8 reps of running" became a stored fact. It then propagated: the next-session proposal
    /// re-served it and the picker showed it back as "3 × 8 · 4d ago", so after one session the
    /// fabrication was indistinguishable from the member's own history.
    ///
    /// WHY NOT OVERLOAD AN EXISTING COLUMN. Storing seconds in RepsCompleted was the cheap option and
    /// is the wrong one: thirteen readers treat that column as reps and exactly none of them check
    /// LoadType. A 90 stored there becomes "1×90" in the timeline, 90 in the Reports Total Reps
    /// column, and 90 against a skill node's MinReps threshold. Overloading converts one required
    /// guard into thirteen, each a future bug. Storing distance in WeightKg has already been tried
    /// and reverted by this project — see 20260812081512_BackfillExerciseLoadTypes, which exists to
    /// delete invented kilograms and whose comment says they "were never a measurement of anything".
    ///
    /// THE BACKFILL IS A DELETION, and it is the same judgement that migration already made about
    /// the same rows. Nulling the reps on Timed and Distance entries is not data loss: the session
    /// survives intact — member, exercise, date and set count are all real — and what disappears is a
    /// number nobody entered. Keeping them beside the new columns would be the worst outcome, because
    /// the database would then hold both a null duration and a fictional 8 for the same run, and every
    /// unguarded reader would go on printing the 8. There is deliberately no attempt to CONVERT them
    /// into durations: nothing in the row carries the information a duration could be derived from,
    /// and inventing one would be the same crime with better manners.
    ///
    /// Down() restores the column's nullability and cannot restore the deleted rep counts, which is
    /// correct for the same reason the earlier migration accepted it. It is a one-way door on live
    /// member history, taken knowingly.
    /// </summary>
    public partial class AddWorkoutLogEntryDurationAndDistance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "RepsCompleted",
                table: "WorkoutLogEntries",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<decimal>(
                name: "DistanceMeters",
                table: "WorkoutLogEntries",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DurationSeconds",
                table: "WorkoutLogEntries",
                type: "integer",
                nullable: true);

            // Scoped by LoadType rather than by exercise name, exactly mirroring the WeightKg cleanup
            // at 20260812081512 — so it also cleans anything a gym has since typed as unloaded itself.
            migrationBuilder.Sql("""
                UPDATE "WorkoutLogEntries" e
                   SET "RepsCompleted" = NULL
                  FROM "Exercises" x
                 WHERE x."Id" = e."ExerciseId"
                   AND x."LoadType" IN ('Timed', 'Distance')
                   AND e."RepsCompleted" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DistanceMeters",
                table: "WorkoutLogEntries");

            migrationBuilder.DropColumn(
                name: "DurationSeconds",
                table: "WorkoutLogEntries");

            migrationBuilder.AlterColumn<int>(
                name: "RepsCompleted",
                table: "WorkoutLogEntries",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
