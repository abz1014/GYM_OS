using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberReferrals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReferredByMemberId",
                table: "Members",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Members_ReferredByMemberId",
                table: "Members",
                column: "ReferredByMemberId");

            migrationBuilder.AddForeignKey(
                name: "FK_Members_Members_ReferredByMemberId",
                table: "Members",
                column: "ReferredByMemberId",
                principalTable: "Members",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Members_Members_ReferredByMemberId",
                table: "Members");

            migrationBuilder.DropIndex(
                name: "IX_Members_ReferredByMemberId",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "ReferredByMemberId",
                table: "Members");
        }
    }
}
