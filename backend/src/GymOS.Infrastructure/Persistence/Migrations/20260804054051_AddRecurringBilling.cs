using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecurringBillingAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberMembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    FailedAttempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LastAttemptDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LastFailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringBillingAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecurringBillingAttempts_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecurringBillingAttempts_MemberMemberships_MemberMembership~",
                        column: x => x.MemberMembershipId,
                        principalTable: "MemberMemberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecurringBillingAttempts_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringBillingAttempts_InvoiceId",
                table: "RecurringBillingAttempts",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringBillingAttempts_MemberId",
                table: "RecurringBillingAttempts",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringBillingAttempts_MemberMembershipId",
                table: "RecurringBillingAttempts",
                column: "MemberMembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringBillingAttempts_Status_NextAttemptDate",
                table: "RecurringBillingAttempts",
                columns: new[] { "Status", "NextAttemptDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecurringBillingAttempts");
        }
    }
}
