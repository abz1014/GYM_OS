using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymOS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Gives a membership a memory of the freeze days it has already spent.
    ///
    /// THE DEFECT. A plan's MaxFreezeDays was checked against one request in isolation and nothing
    /// recorded what had been used, while resuming credited the entire requested window back onto
    /// EndDate whether or not any of it had elapsed. Freezing and resuming the same untouched future
    /// window therefore paid out every time. Measured on this database before the fix: three cycles
    /// of one 30-day window moved a paid-up-to date from 2027-07-21 to 2027-10-19 — ninety days of
    /// membership created from nothing, unbounded.
    ///
    /// WHY EXISTING ROWS START AT ZERO, which is a real decision and not just the column default.
    /// Freezes that were resumed before this column existed left no record of what they were credited
    /// — FreezeStartDate and FreezeEndDate were overwritten by the next freeze and never cleared, so
    /// history cannot be reconstructed, and inventing a number for it would be worse than admitting
    /// the gap. Everyone therefore begins with their full allowance intact. That errs toward the
    /// member, which is the right direction to err: the alternative is telling somebody they have
    /// used days that this system cannot show them.
    ///
    /// Memberships frozen RIGHT NOW are also correct at zero — they have not been resumed, so nothing
    /// has been credited to them yet, and their in-flight window is still on the row.
    /// </summary>
    public partial class AddMembershipFreezeDaysUsed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FreezeDaysUsed",
                table: "MemberMemberships",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dropping this returns the allowance to being per-request, which is the defect. It loses
            // no member-visible data: EndDate already carries every day that was ever credited.
            migrationBuilder.DropColumn(
                name: "FreezeDaysUsed",
                table: "MemberMemberships");
        }
    }
}
