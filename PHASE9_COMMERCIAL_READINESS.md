# Phase 9 — Commercial Readiness

> Answers to the seven questions in GYMOS_NEXT_PHASE_EXECUTION_GUIDE.md Phase 9. Every claim is
> tied to a capability that exists and was verified in this codebase (phases 1–8), not to a
> roadmap item. Simulated-by-scope items are called out explicitly at the end.

## 1. Why would a gym migrate?

- **Migration is a product feature, not a services engagement.** The Migration Center takes a
  CSV through upload → field mapping → validation → preview → commit → rollback for Members,
  Trainers, Equipment, and Inventory — and commits through the same create commands the UI uses,
  so imported data passes identical validation. A prospect can watch their own spreadsheet
  become working data during the sales demo.
- **One system instead of five.** Membership billing, attendance, CRM pipeline, trainer
  commissions, equipment maintenance, and retail inventory live in one permission model with one
  audit trail, replacing the typical spreadsheet-plus-point-tools stack.
- **Multi-branch from day one.** Branch scoping is built into the schema and the UI (branch
  switcher, per-branch staff access via UserBranchAccess), not a bolted-on filter.

## 2. Why would they stay?

- **The audit trail compounds.** Every business action is recorded atomically with the change
  itself (pipeline behavior inside the same DB transaction) — the longer the system runs, the
  more operational history it holds that a replacement wouldn't have.
- **Their data is not hostage.** Every report exports to Excel; the import framework proves the
  schema is documented and mappable. (Honest note: a full raw-data export tool is not built —
  see gaps.)
- **Self-service control.** The permission matrix is editable in-app per role; branches, gym
  profile, and system preferences are owner-managed without vendor involvement.

## 3. What differentiates GymOS?

- **Auditability by construction** (Non-Negotiable #4): no per-feature opt-in; any new command
  is audited automatically, including redaction of sensitive fields — verified live for the
  whole auth surface.
- **Tenant isolation enforced at the ORM layer** and proven by automated tests
  (`TenantIsolationTests`), not by convention in each query.
- **API-first**: all 121 gated endpoints are documented in Swagger; anything the UI does, an
  integrator can do.
- **Swap-in integrations**: payments, email/SMS/WhatsApp, object storage, door access are
  interfaces with demo implementations — going live with Stripe or Twilio is configuration plus
  one adapter, with zero business-logic changes (verified by Phase 7's dependency audit).

## 4. What reduces operational effort?

- **Eight recurring jobs** do the daily chasing: membership expiry checks and transitions,
  invoice overdue transitions, low-stock alerts, maintenance-due alerts, birthdays, CRM
  follow-up reminders, notification dispatch.
- **Alerts that don't spam**: low-stock notifies once per shortfall (flag resets on restock —
  re-verified live this phase); maintenance notifies once per due cycle.
- **Live dashboard** via SignalR — a front-desk check-in updates the owner's screen without a
  refresh.
- **Role-shaped screens**: eight seeded roles see only their modules (verified for Receptionist
  on desktop and in the mobile drawer).

## 5. What improves retention?

- **Expiry is visible before it happens**: "Expiring in 7 days" on the dashboard plus daily
  expiry-check notifications give staff a save-the-member window.
- **Freeze/resume** offers a middle path instead of cancellation, with plan-level freeze-day
  limits.
- **Touchpoints**: birthday and follow-up reminder jobs feed the notification center.
- **Engagement surface**: workout and nutrition logging exist per member (functional today;
  flagged Foundation for aggregate operations in Phase 8).

## 6. What increases revenue?

- **Renewal always produces an invoice** — the renewal workflow creates the invoice in the same
  transaction, so revenue tracking can't drift from membership state (tested, including
  rollback atomicity).
- **Discount/coupon engine** with redemption caps and validity windows, correct to the cent in
  tests (20% coupon → 120.00 on a 150.00 plan, discount itemized on the invoice).
- **Trainer commissions tracked** per period with pending/paid states and a report — makes PT
  revenue manageable.
- **CRM pipeline with measured conversion** (stage funnel + conversion rate report) turns lead
  handling into a number that can be improved.
- **Retail-ready invoicing**: product-sale line items exist on invoices; inventory tracks unit
  cost vs price.

## 7. What lowers maintenance cost?

- **Governed architecture** (Phase 7 verified): strict layer direction, controllers as thin
  pass-throughs, single implementations for audit writing, stock adjustment, permission
  resolution, and notification recipients — one place to change each rule.
- **36 automated tests** across domain/application/API catch regressions in the riskiest paths
  (tenant isolation, auth/MFA, renewal+invoice atomicity, permission enforcement).
- **Versioned EF migrations** and a health endpoint (`/health`, DB-backed) for deployment ops.
- **Demo data as a fixture**: deterministic seeding rebuilds a full realistic environment on
  demand.

## Gaps to close before charging money

These are known and scoped, not discovered problems:

1. **Real integrations**: payment gateway, email/SMS/WhatsApp senders, and QR hardware are
   no-op demo implementations behind interfaces (by explicit scope decision).
2. **Ops hardening**: backup/restore runbook and production monitoring/alerting are not set up;
   login has no brute-force lockout or failed-attempt audit (success-path auditing only).
3. **Scale polish**: Leads / Work Orders / Assets lists need server-side pagination for large
   deployments; Workouts/Nutrition need aggregate reporting/export/import (Phase 8 verdict).
4. **Compliance**: medical notes are stored unencrypted at the column level — flagged since the
   original plan as a pre-real-data requirement.
