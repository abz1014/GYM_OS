using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberProgressionPeakXp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "PeakXp",
                table: "MemberProgressions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            // The column default of 0 is right for a row that does not exist yet and catastrophic for
            // every row that already does: Level derives from PeakXp, so shipping this without a
            // backfill would demote every existing member to level 1 on deploy — the exact demotion
            // this whole change exists to make impossible.
            //
            // TotalXp is the correct seed because it is the ledger's sum, and before this migration
            // nothing could lower it except an undo inside a few-minute window. Anyone mid-undo at
            // deploy time loses at most one session's worth, and gains a floor from then on.
            migrationBuilder.Sql(@"UPDATE ""MemberProgressions"" SET ""PeakXp"" = ""TotalXp"";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PeakXp",
                table: "MemberProgressions");
        }
    }
}
