using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymOS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// One active place per member per session, enforced by the database.
    ///
    /// THE DEFECT. The booking handler's "already booked" guard is a read followed by a write, and
    /// under concurrency every racer passes the read before any of them commits. Observed live: one
    /// member holding THREE simultaneous Booked rows on one session — rendered three times over on
    /// their own membership page — plus a capacity-2 session confirming 6. The handler now also takes
    /// a per-session advisory lock (the capacity half of the race); this index is the structural
    /// guarantee for the duplicate half, which no application code can provide.
    ///
    /// THE CLEANUP FIRST. A unique index cannot be created over rows that already violate it, so the
    /// duplicates the race left behind are settled before the index lands: per (session, member) the
    /// best-standing row survives — CheckedIn beats Booked beats Waitlisted (attendance evidence
    /// wins), earliest BookedAt breaks ties — and the rest are marked Cancelled, which also removes
    /// them from every roster count and member screen. Cancelled/NoShow rows stay outside the index
    /// filter on purpose: rebooking after cancelling, or after missing a class, is normal life.
    /// </summary>
    public partial class AddOneActiveBookingPerMemberSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "ClassBookings" SET "Status" = 'Cancelled', "CancelledAt" = now()
                WHERE "Id" IN (
                    SELECT "Id" FROM (
                        SELECT "Id", ROW_NUMBER() OVER (
                            PARTITION BY "ClassSessionId", "MemberId"
                            ORDER BY CASE "Status" WHEN 'CheckedIn' THEN 0 WHEN 'Booked' THEN 1 ELSE 2 END, "BookedAt"
                        ) AS rn
                        FROM "ClassBookings"
                        WHERE "Status" IN ('Booked', 'Waitlisted', 'CheckedIn')
                    ) ranked
                    WHERE ranked.rn > 1
                );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ClassBookings_OneActivePerMemberSession",
                table: "ClassBookings",
                columns: new[] { "ClassSessionId", "MemberId" },
                unique: true,
                filter: "\"Status\" IN ('Booked', 'Waitlisted', 'CheckedIn')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ClassBookings_OneActivePerMemberSession",
                table: "ClassBookings");
        }
    }
}
