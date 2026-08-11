using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRankPromotions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RankPromotions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tier = table.Column<string>(type: "text", nullable: false),
                    AchievedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Seen = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RankPromotions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RankPromotions_MemberId_Seen",
                table: "RankPromotions",
                columns: new[] { "MemberId", "Seen" });

            migrationBuilder.CreateIndex(
                name: "IX_RankPromotions_MemberId_Tier",
                table: "RankPromotions",
                columns: new[] { "MemberId", "Tier" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RankPromotions");
        }
    }
}
