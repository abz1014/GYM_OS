namespace GymOS.Infrastructure.Persistence;

/// <summary>
/// Database-level guards that the EF model cannot express, kept somewhere a test harness can apply
/// them.
///
/// A migration is the production path for this DDL, and a migration is an immutable historical
/// record — so the SQL is duplicated there rather than referenced from here, deliberately. What this
/// class exists for is the other path: harnesses that build a schema with EnsureCreated, which
/// ignores migrations entirely and would therefore test a database missing every guard production
/// has. Switching those harnesses to Migrate() was the obvious alternative and is much slower, since
/// each fixture would replay the whole migration history against a freshly dropped database.
///
/// If the trigger below is ever changed, change it in a NEW migration and update this copy to match.
/// The integration suite has a test that overpays through raw SQL, so a copy that has drifted out of
/// step with the migration shows up as a failing test rather than as a quiet difference.
/// </summary>
public static class BillingGuards
{
    /// <summary>
    /// Mirrors migration 20260812064646_AddOverpaymentGuardTrigger. Idempotent — CREATE OR REPLACE
    /// plus a DROP guard — so applying it to a database that already has it is a no-op.
    /// </summary>
    public const string OverpaymentTriggerSql = """
        CREATE OR REPLACE FUNCTION gymos_reject_overpayment() RETURNS trigger AS $$
        DECLARE
            v_total    numeric(18,2);
            v_paid     numeric(18,2);
            v_refunded numeric(18,2);
        BEGIN
            SELECT "TotalAmount" INTO v_total FROM "Invoices" WHERE "Id" = NEW."InvoiceId";

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

        DROP TRIGGER IF EXISTS gymos_payments_overpayment_guard ON "Payments";
        CREATE CONSTRAINT TRIGGER gymos_payments_overpayment_guard
            AFTER INSERT OR UPDATE ON "Payments"
            DEFERRABLE INITIALLY IMMEDIATE
            FOR EACH ROW EXECUTE FUNCTION gymos_reject_overpayment();
        """;
}
