using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TrainerMemberRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Trainers_Users_UserId",
                table: "Trainers");

            migrationBuilder.AlterColumn<string>(
                name: "Specialties",
                table: "Trainers",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "LastName",
                table: "Leads",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "FirstName",
                table: "Leads",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Leads",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "LeadActivities",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "IX_TrainerRatings_MemberId",
                table: "TrainerRatings",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainerAssignments_MemberId",
                table: "TrainerAssignments",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_TenantId_Stage",
                table: "Leads",
                columns: new[] { "TenantId", "Stage" });

            migrationBuilder.AddForeignKey(
                name: "FK_TrainerAssignments_Members_MemberId",
                table: "TrainerAssignments",
                column: "MemberId",
                principalTable: "Members",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TrainerRatings_Members_MemberId",
                table: "TrainerRatings",
                column: "MemberId",
                principalTable: "Members",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Trainers_Users_UserId",
                table: "Trainers",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TrainerAssignments_Members_MemberId",
                table: "TrainerAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_TrainerRatings_Members_MemberId",
                table: "TrainerRatings");

            migrationBuilder.DropForeignKey(
                name: "FK_Trainers_Users_UserId",
                table: "Trainers");

            migrationBuilder.DropIndex(
                name: "IX_TrainerRatings_MemberId",
                table: "TrainerRatings");

            migrationBuilder.DropIndex(
                name: "IX_TrainerAssignments_MemberId",
                table: "TrainerAssignments");

            migrationBuilder.DropIndex(
                name: "IX_Leads_TenantId_Stage",
                table: "Leads");

            migrationBuilder.AlterColumn<string>(
                name: "Specialties",
                table: "Trainers",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300);

            migrationBuilder.AlterColumn<string>(
                name: "LastName",
                table: "Leads",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "FirstName",
                table: "Leads",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Leads",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "LeadActivities",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AddForeignKey(
                name: "FK_Trainers_Users_UserId",
                table: "Trainers",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
