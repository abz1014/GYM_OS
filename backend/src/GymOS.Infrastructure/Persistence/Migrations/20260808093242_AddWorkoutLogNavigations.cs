using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkoutLogNavigations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_WorkoutLogs_WorkoutTemplateId",
                table: "WorkoutLogs",
                column: "WorkoutTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutLogEntries_ExerciseId",
                table: "WorkoutLogEntries",
                column: "ExerciseId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkoutLogEntries_Exercises_ExerciseId",
                table: "WorkoutLogEntries",
                column: "ExerciseId",
                principalTable: "Exercises",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkoutLogs_WorkoutTemplates_WorkoutTemplateId",
                table: "WorkoutLogs",
                column: "WorkoutTemplateId",
                principalTable: "WorkoutTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkoutLogEntries_Exercises_ExerciseId",
                table: "WorkoutLogEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkoutLogs_WorkoutTemplates_WorkoutTemplateId",
                table: "WorkoutLogs");

            migrationBuilder.DropIndex(
                name: "IX_WorkoutLogs_WorkoutTemplateId",
                table: "WorkoutLogs");

            migrationBuilder.DropIndex(
                name: "IX_WorkoutLogEntries_ExerciseId",
                table: "WorkoutLogEntries");
        }
    }
}
