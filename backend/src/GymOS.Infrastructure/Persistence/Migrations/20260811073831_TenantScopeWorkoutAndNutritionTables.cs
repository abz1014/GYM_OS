using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TenantScopeWorkoutAndNutritionTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "WorkoutLogs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "WorkoutLogEntries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "WorkoutAssignments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "WaterLogs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "RecoveryLogs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "MealEntries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "DietPlans",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
            /*
             * Backfill, and this migration is dangerous without it.
             *
             * These tables now carry a global query filter of TenantId == CurrentTenantId. EF defaults
             * the new column to an all-zero Guid, which matches no tenant — so shipping the column
             * alone would make every existing workout, meal, plan and rest day vanish from the API at
             * once. Nothing would error; the data would simply stop being returned.
             *
             * Each row takes the tenant of whatever already owns it, which is the only honest source:
             * five from their Member, MealEntries from their DietPlan, and WorkoutLogEntries from the
             * log they belong to — neither of those two has a MemberId of its own.
             */
            migrationBuilder.Sql(@"
                UPDATE ""WorkoutLogs"" l        SET ""TenantId"" = m.""TenantId"" FROM ""Members"" m     WHERE l.""MemberId"" = m.""Id"";
                UPDATE ""WorkoutAssignments"" a SET ""TenantId"" = m.""TenantId"" FROM ""Members"" m     WHERE a.""MemberId"" = m.""Id"";
                UPDATE ""RecoveryLogs"" r       SET ""TenantId"" = m.""TenantId"" FROM ""Members"" m     WHERE r.""MemberId"" = m.""Id"";
                UPDATE ""WaterLogs"" w          SET ""TenantId"" = m.""TenantId"" FROM ""Members"" m     WHERE w.""MemberId"" = m.""Id"";
                UPDATE ""DietPlans"" d          SET ""TenantId"" = m.""TenantId"" FROM ""Members"" m     WHERE d.""MemberId"" = m.""Id"";
                UPDATE ""MealEntries"" e        SET ""TenantId"" = d.""TenantId"" FROM ""DietPlans"" d   WHERE e.""DietPlanId"" = d.""Id"";
                UPDATE ""WorkoutLogEntries"" e  SET ""TenantId"" = l.""TenantId"" FROM ""WorkoutLogs"" l WHERE e.""WorkoutLogId"" = l.""Id"";
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "WorkoutLogs");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "WorkoutLogEntries");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "WorkoutAssignments");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "WaterLogs");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "RecoveryLogs");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "MealEntries");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "DietPlans");
        }
    }
}
