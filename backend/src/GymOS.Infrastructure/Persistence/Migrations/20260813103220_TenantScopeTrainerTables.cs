using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymOS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Brings the last three unscoped tables under the tenant filter.
    ///
    /// THE DEFECT. An EF global query filter attaches to an entity only if the entity carries the
    /// column it filters on. TrainerSessions, TrainerAssignments and CommissionRecords carried
    /// neither TenantId nor BranchId — confirmed by reading information_schema, not by inference — so
    /// no filter could apply to them and their handlers loaded rows by raw id. On a single-tenant
    /// database that is invisible, which is precisely why it survived: every test passes, every
    /// screen looks right, and the hole opens the day a second gym is onboarded, as a cross-tenant
    /// read AND write on commission money and session history.
    ///
    /// This is the same fix, for the same reason, as 20260811073831_TenantScopeWorkoutAndNutritionTables
    /// applied to the workout and nutrition tables — the trainer tables were simply missed.
    ///
    /// WHY TENANT AND NOT BRANCH. A trainer belongs to a branch, but their assignments, sessions and
    /// commission history follow the trainer rather than a site, and the staff who manage them are
    /// branch-mobile. Tenant is the boundary that must never be crossed; branch is still reachable
    /// through Trainer, which is IBranchScoped already. Adding a branch column here would silently
    /// hide a trainer's own history from a manager standing at a different desk.
    ///
    /// THE BACKFILL IS THE LOAD-BEARING PART. Adding the column alone would leave every existing row
    /// at Guid.Empty, and the tenant filter FAILS CLOSED — it matches nothing when the ids disagree —
    /// so every historical assignment, session and commission record would vanish from the app the
    /// moment this deployed. Each row's tenant is derived from the row it already belongs to:
    /// assignments and commissions from their Trainer, sessions from their (now-backfilled)
    /// assignment. Ordering matters: sessions must run last.
    ///
    /// Down() drops the columns, which returns the tables to being unfiltered. That restores the
    /// defect rather than losing data, and is the honest inverse of what Up() does.
    /// </summary>
    public partial class TenantScopeTrainerTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "TrainerSessions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "TrainerAssignments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "CommissionRecords",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Derived from the owning row, never guessed. Sessions come last because they inherit
            // from TrainerAssignments, which is only correct after the line above it has run.
            migrationBuilder.Sql(@"
                UPDATE ""TrainerAssignments"" a SET ""TenantId"" = t.""TenantId"" FROM ""Trainers"" t            WHERE a.""TrainerId"" = t.""Id"";
                UPDATE ""CommissionRecords""  c SET ""TenantId"" = t.""TenantId"" FROM ""Trainers"" t            WHERE c.""TrainerId"" = t.""Id"";
                UPDATE ""TrainerSessions""    s SET ""TenantId"" = a.""TenantId"" FROM ""TrainerAssignments"" a  WHERE s.""TrainerAssignmentId"" = a.""Id"";
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "TrainerSessions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "TrainerAssignments");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CommissionRecords");
        }
    }
}
