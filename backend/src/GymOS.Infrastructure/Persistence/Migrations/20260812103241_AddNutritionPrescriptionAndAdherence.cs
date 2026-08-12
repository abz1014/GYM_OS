using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNutritionPrescriptionAndAdherence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "DietPlans",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DietPlanGuidance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DietPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Cadence = table.Column<string>(type: "text", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "text", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DietPlanGuidance", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DietPlanGuidance_DietPlans_DietPlanId",
                        column: x => x.DietPlanId,
                        principalTable: "DietPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlanAdherenceLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    DietPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    OnDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LoggedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanAdherenceLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DietPlans_MemberId_StartDate",
                table: "DietPlans",
                columns: new[] { "MemberId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DietPlanGuidance_DietPlanId_Cadence_EffectiveFrom",
                table: "DietPlanGuidance",
                columns: new[] { "DietPlanId", "Cadence", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_PlanAdherenceLogs_MemberId_OnDate",
                table: "PlanAdherenceLogs",
                columns: new[] { "MemberId", "OnDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DietPlanGuidance");

            migrationBuilder.DropTable(
                name: "PlanAdherenceLogs");

            migrationBuilder.DropIndex(
                name: "IX_DietPlans_MemberId_StartDate",
                table: "DietPlans");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "DietPlans");
        }
    }
}
