using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDietPlanMacroTargets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TargetCarbsG",
                table: "DietPlans",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TargetFatG",
                table: "DietPlans",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TargetProteinG",
                table: "DietPlans",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetCarbsG",
                table: "DietPlans");

            migrationBuilder.DropColumn(
                name: "TargetFatG",
                table: "DietPlans");

            migrationBuilder.DropColumn(
                name: "TargetProteinG",
                table: "DietPlans");
        }
    }
}
