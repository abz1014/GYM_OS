using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymOS.Infrastructure.Persistence.Migrations;

/// <summary>
/// A database-level backstop for the one invariant that protects the money: the completed payments
/// on an invoice, less completed refunds, may never exceed its total.
///
/// The rule already lives in the application, in four handlers, each of which can be forgotten. It
/// was forgotten twice: ImportPaymentCommand never had it, and RecurringBillingJob wrote a payment
/// and declared the invoice Paid without reading what was already on it. Neither needed concurrency
/// to go wrong — just a code path nobody re-read. A guard that lives beside the data survives the
/// next handler somebody adds.
///
/// WHAT THIS DOES NOT DO, so nobody mistakes it for the whole fix: it does not close the race. The
/// trigger runs inside the inserting transaction under READ COMMITTED, so two simultaneous inserts
/// each see their own row plus what was committed before they began — neither sees the other, and
/// both pass. Serialising them is the row lock's job (IApplicationDbContext.LockInvoiceForUpdateAsync);
/// with that lock in place the second transaction runs after the first commits and this trigger then
/// sees the whole picture. The two are complementary, and only together are they sufficient.
///
/// Written as raw SQL in a migration rather than as a model-level check constraint because the
/// invariant spans rows: a CHECK cannot see sibling payments, and the alternative — a maintained
/// AmountPaid column — would create a second answer to "what has been paid" that can drift from the
/// payment rows, which is a worse failure than the one being prevented.
/// </summary>
public partial class AddOverpaymentGuardTrigger : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Status columns are stored as text (see the enum conversions in GymOsDbContext), so these
        // compare against the enum NAMES rather than ordinals.
        migrationBuilder.Sql("""
            CREATE OR REPLACE FUNCTION gymos_reject_overpayment() RETURNS trigger AS $$
            DECLARE
                v_total    numeric(18,2);
                v_paid     numeric(18,2);
                v_refunded numeric(18,2);
            BEGIN
                SELECT "TotalAmount" INTO v_total FROM "Invoices" WHERE "Id" = NEW."InvoiceId";

                -- No invoice means a foreign key is about to complain about something more basic.
                IF v_total IS NULL THEN
                    RETURN NEW;
                END IF;

                SELECT COALESCE(sum("Amount"), 0) INTO v_paid
                  FROM "Payments"
                 WHERE "InvoiceId" = NEW."InvoiceId" AND "Status" = 'Completed';

                SELECT COALESCE(sum(r."Amount"), 0) INTO v_refunded
                  FROM "Refunds" r
                  JOIN "Payments" p ON p."Id" = r."PaymentId"
                 WHERE p."InvoiceId" = NEW."InvoiceId" AND r."Status" = 'Completed';

                IF (v_paid - v_refunded) > v_total THEN
                    RAISE EXCEPTION
                        'Payments on invoice % would total %, which exceeds its total of %',
                        NEW."InvoiceId", (v_paid - v_refunded), v_total
                        USING ERRCODE = 'check_violation';
                END IF;

                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;
            """);

        // AFTER, not BEFORE: the row being inserted has to be part of the sum it is checked against.
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS gymos_payments_overpayment_guard ON "Payments";
            CREATE CONSTRAINT TRIGGER gymos_payments_overpayment_guard
                AFTER INSERT OR UPDATE ON "Payments"
                DEFERRABLE INITIALLY IMMEDIATE
                FOR EACH ROW EXECUTE FUNCTION gymos_reject_overpayment();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""DROP TRIGGER IF EXISTS gymos_payments_overpayment_guard ON "Payments";""");
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS gymos_reject_overpayment();");
    }
}
