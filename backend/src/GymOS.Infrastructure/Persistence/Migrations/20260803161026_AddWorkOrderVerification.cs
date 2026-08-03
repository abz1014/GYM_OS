using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkOrderVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MaintenanceScheduleId",
                table: "WorkOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationNotes",
                table: "WorkOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "VerifiedAt",
                table: "WorkOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VerifiedByUserId",
                table: "WorkOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_MaintenanceScheduleId",
                table: "WorkOrders",
                column: "MaintenanceScheduleId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrders_MaintenanceSchedules_MaintenanceScheduleId",
                table: "WorkOrders",
                column: "MaintenanceScheduleId",
                principalTable: "MaintenanceSchedules",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrders_MaintenanceSchedules_MaintenanceScheduleId",
                table: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_MaintenanceScheduleId",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "MaintenanceScheduleId",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "VerificationNotes",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "VerifiedAt",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "VerifiedByUserId",
                table: "WorkOrders");
        }
    }
}
