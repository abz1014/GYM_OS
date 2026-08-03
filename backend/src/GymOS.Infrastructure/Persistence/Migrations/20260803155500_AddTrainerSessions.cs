using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTrainerSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                table: "TrainerRatings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TrainerSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TrainerAssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainerSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainerSessions_TrainerAssignments_TrainerAssignmentId",
                        column: x => x.TrainerAssignmentId,
                        principalTable: "TrainerAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrainerRatings_SessionId",
                table: "TrainerRatings",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainerSessions_TrainerAssignmentId",
                table: "TrainerSessions",
                column: "TrainerAssignmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_TrainerRatings_TrainerSessions_SessionId",
                table: "TrainerRatings",
                column: "SessionId",
                principalTable: "TrainerSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TrainerRatings_TrainerSessions_SessionId",
                table: "TrainerRatings");

            migrationBuilder.DropTable(
                name: "TrainerSessions");

            migrationBuilder.DropIndex(
                name: "IX_TrainerRatings_SessionId",
                table: "TrainerRatings");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "TrainerRatings");
        }
    }
}
