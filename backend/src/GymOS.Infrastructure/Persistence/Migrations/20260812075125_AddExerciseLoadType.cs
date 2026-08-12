using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExerciseLoadType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LoadType",
                table: "Exercises",
                type: "text",
                nullable: false,
                // "Weighted", not "" — the column stores the enum by NAME (see the converter in
                // GymOsDbContext), and an empty string parses back to nothing. Every existing row is
                // a barbell-or-machine movement as far as the old code was concerned, so Weighted is
                // both the safe default and the truthful one. A gym re-types its treadmills after
                // this lands; it does not lose its bench presses to a blank.
                defaultValue: "Weighted");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LoadType",
                table: "Exercises");
        }
    }
}
