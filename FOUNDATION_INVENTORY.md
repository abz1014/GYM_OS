# GymOS — Foundation Inventory
### Execution Guide Phase 2 Deliverable

> **Purpose.** Per `GYMOS_NEXT_PHASE_EXECUTION_GUIDE.md` Phase 2, this document inventories every module that **currently exists** in the codebase — no new modules are proposed or added here (Classes, Bookings, POS, Accounting, HR, Payroll, and Marketing are out of scope per the user's explicit scope decision recorded in `GAP_VALIDATION_REPORT.md`, and are not modules that exist today regardless). Each module below is scored on the 10 dimensions the guide specifies. Ratings use a 0–10 scale for consistency with `CURRENT_SYSTEM_ANALYSIS.md`/`TARGET_GYMOS_GAP_ANALYSIS.md`; every rating and every "Missing Pieces" line is grounded in the file-level evidence already established in those two documents and re-confirmed in `GAP_VALIDATION_REPORT.md`.
>
> **Reading guide**: *Workflow Maturity* measures whether a module's business process runs start-to-finish without a dead end (independent of how much UI exists — a module can have great screens but a broken workflow, or vice versa). *Production Readiness* measures whether the module, as it stands, could be trusted to run a real gym's operations today. *Commercial Readiness* measures whether the module would survive a serious sales evaluation against a named competitor.

---

## 1. Authentication

- **Current Purpose**: JWT-based login/session management for staff (and, in principle, member) users, with refresh-token rotation, forgot/reset/change-password flows.
- **Backend Maturity**: 8/10 — Login, refresh-token rotation, forgot-password, reset-password, and change-password are all implemented, validated, and correctly avoid account-existence leakage. MFA (TOTP) is fully implemented in `TotpService` but has no enablement code path anywhere.
- **Frontend Maturity**: 7/10 — LoginPage and ForgotPasswordPage both work cleanly; no MFA enrollment UI, no active-session management UI, no SSO option.
- **Workflow Maturity**: 7/10 — Login → session → silent refresh → logout is a complete, correct loop. MFA enrollment is not a partial workflow — it does not exist at all (no command sets `MfaEnabled`/`MfaSecret`).
- **Testing Status**: 0/10 — zero automated tests for any Auth command or validator.
- **Documentation Status**: 4/10 — README documents demo credentials and setup; no dedicated auth-flow design document exists; inline code comments are frequent and genuinely helpful.
- **Production Readiness**: 4/10 — blocked by `localStorage` token storage (XSS-exposed), no MFA enforcement path, no login rate-limiting/lockout.
- **Commercial Readiness**: 5/10 — sufficient for a single small gym's internal staff login; not enterprise-credible without MFA and SSO.
- **Dependencies**: RBAC (permission resolution runs post-authentication), Settings (future home for a per-tenant MFA-required policy).
- **Missing Pieces**: MFA enrollment flow + UI; SSO/OAuth for staff; active-session list/revoke UI; login rate-limiting/lockout; cookie-based (httpOnly) token storage.

---

## 2. RBAC (Role-Based Access Control)

- **Current Purpose**: Gate every API action behind one of 37 permission codes assigned to 8 fixed roles.
- **Backend Maturity**: 8/10 — one authorization policy registered per permission code via reflection; per-request permission resolution via middleware; correctly enforced on nearly every controller action.
- **Frontend Maturity**: 6/10 — the sidebar correctly hides nav items the current user lacks permission for; no UI exists to view or edit which role has which permission.
- **Workflow Maturity**: 5/10 — the *checking* workflow is solid and consistent; there is no *management* workflow at all (roles are hardcoded, not creatable/editable).
- **Testing Status**: 0/10.
- **Documentation Status**: 5/10 — the permission catalog is self-documenting (each code carries a human-readable description string, seeded into the `Permissions` table); no external RBAC design document exists.
- **Production Readiness**: 6/10 — solid enforcement mechanism undermined by 3 permission codes (`settings.view`, `settings.manage_branches`, `settings.manage_gym_profile`) that are seeded and assigned to roles but checked by zero controller actions, and one endpoint (`BranchesController.List`) with no permission check of any kind.
- **Commercial Readiness**: 4/10 — the fixed 8-role model will not satisfy customers who need custom roles; no self-service permission administration exists.
- **Dependencies**: Settings (the permission-matrix editor's natural home).
- **Missing Pieces**: custom-role CRUD; a permission-matrix editor UI; enforcement of the 3 orphaned Settings permission codes; a permission check added to `BranchesController.List`.

---

## 3. Dashboard

- **Current Purpose**: At-a-glance daily operational KPIs for staff/owners.
- **Backend Maturity**: 6/10 — 6 of 10 KPI fields are genuinely computed from real data; the other 4 (`TrainerScheduleTodayCount`, `EquipmentAlertsCount`, `MaintenanceRemindersCount`, `InventoryAlertsCount`) are hardcoded to `0` in the query handler.
- **Frontend Maturity**: 7/10 — clean stat-card layout; live SignalR-driven refresh on check-in/payment events genuinely works; a static placeholder text block still references "Wave 2" as future work despite those modules having shipped.
- **Workflow Maturity**: N/A — a read-only reporting surface, not a transactional workflow.
- **Testing Status**: 0/10.
- **Documentation Status**: 3/10.
- **Production Readiness**: 6/10 — functionally fine for the 6 live fields; the 4 dead fields and the stale copy are the kind of defect a discerning evaluator notices immediately.
- **Commercial Readiness**: 5/10.
- **Dependencies**: Trainers, Equipment, Maintenance, Inventory (all four already have the query logic needed to feed the dead KPI fields today).
- **Missing Pieces**: wire the 4 hardcoded fields to existing Trainers/Equipment/Maintenance/Inventory queries; refresh the stale UI copy; role-specific dashboard variants (owner vs. front-desk vs. trainer).

---

## 4. Members

- **Current Purpose**: Member registration, search, profile management, and membership-history tracking — the operational hub of the system.
- **Backend Maturity**: 8/10 — create/update/renew/freeze/transfer commands plus 4 record-add commands (emergency contact, medical note, measurement, progress photo) all exist and are validated; no delete command exists anywhere.
- **Frontend Maturity**: 6/10 — list/detail/create/renew/freeze/transfer all work well; edit-profile UI and all 4 add-record UIs are entirely absent despite the backend being fully ready for them.
- **Workflow Maturity**: 5/10 — registration → renewal → freeze → transfer is a complete, correct chain; there is no un-freeze path (no command exists, not just no UI), no cancellation-reason capture, and no delete/GDPR-erasure path.
- **Testing Status**: 0/10.
- **Documentation Status**: 3/10.
- **Production Readiness**: 6/10 — the missing edit-profile capability is an immediate, daily-operational gap (a front-desk worker cannot currently correct a typo in a member's name or email through the product).
- **Commercial Readiness**: 6/10 — the single most complete module in the system by feature depth; closing its frontend parity gap should be an early, high-leverage action.
- **Dependencies**: File storage (for profile/progress photos, once wired up), Settings (branch data referenced throughout).
- **Missing Pieces**: edit-profile UI; 4 add-record UIs; a delete/soft-delete command + UI with GDPR-export consideration; an un-freeze command + UI; an explicit reactivation workflow distinct from a plain renewal.

---

## 5. Memberships

- **Current Purpose**: Membership plan catalog plus discount/coupon management.
- **Backend Maturity**: 7/10 — plans have full CRUD; discounts and coupons are create-only with **no list/view query at all**.
- **Frontend Maturity**: 5/10 — the plan catalog + creation UI is solid; there is zero UI for discounts/coupons, consistent with there being no query to build a UI against.
- **Workflow Maturity**: 4/10 — a coupon can be created and successfully redeemed inside `RenewMembershipCommand`, but staff can never see what coupons exist, how many times one has been used, or deactivate one — a genuinely broken administrative loop, not just a missing nicety.
- **Testing Status**: 0/10.
- **Documentation Status**: 3/10.
- **Production Readiness**: 5/10 — the write-only discount/coupon trap is a real defect.
- **Commercial Readiness**: 5/10.
- **Dependencies**: Members (redemption happens inside the membership-renewal flow).
- **Missing Pieces**: `GetDiscountsQuery`/`GetCouponsQuery` plus a full list/edit/deactivate UI for both.

---

## 6. Attendance

- **Current Purpose**: Check-in/check-out tracking and visit-pattern analytics.
- **Backend Maturity**: 7/10 — check-in, check-out, and a 24-bucket peak-hours query all exist and are correct.
- **Frontend Maturity**: 5/10 — check-in (simulated QR search-and-click) works well; check-out has zero UI (every record's check-out time permanently displays "—"); the peak-hours chart doesn't exist anywhere in the frontend.
- **Workflow Maturity**: 5/10 — the check-in half of the workflow is complete and correct; the check-out half is a dead end from the UI's perspective, even though the backend supports it.
- **Testing Status**: 0/10.
- **Documentation Status**: 3/10.
- **Production Readiness**: 5/10.
- **Commercial Readiness**: 5/10.
- **Dependencies**: Members (the check-in target).
- **Missing Pieces**: check-out UI; a peak-hours chart surfacing the existing query; capacity-limit alerting.

---

## 7. Billing

- **Current Purpose**: Invoice creation and payment recording.
- **Backend Maturity**: 7/10 — invoice, payment, and refund commands all exist and are correct; `PaymentReminder` exists as a table with zero code ever writing to or processing it.
- **Frontend Maturity**: 6/10 — invoice list/detail/create and payment-recording all work; refund has zero UI despite the command existing.
- **Workflow Maturity**: 5/10 — create-invoice → record-payment works end to end; refund is a UI dead end; there is no recurring-billing scheduler at all (every invoice is a manual, one-off staff action) and no dunning/retry logic — **the real payment-gateway integration itself is explicitly [DEFERRED]** per user direction, but the refund-UI gap is independent of that and remains in-scope.
- **Testing Status**: 0/10.
- **Documentation Status**: 3/10.
- **Production Readiness**: 3/10 — hard-blocked by the simulated payment gateway (deferred by design), independent of the in-scope refund-UI gap.
- **Commercial Readiness**: 3/10 (gateway question aside).
- **Dependencies**: Members. **[DEFERRED dependency: a real payment gateway.]**
- **Missing Pieces (in-scope)**: refund UI. **(Out of scope per direction: real gateway integration, recurring-billing scheduler, dunning automation.)**

---

## 8. CRM & Leads

- **Current Purpose**: Lead capture and sales-pipeline tracking.
- **Backend Maturity**: 7/10 — full lead/activity CRUD plus a pipeline-summary aggregation query; the stage-transition command does not create or link an actual `Member` record when a lead reaches the `Member` stage.
- **Frontend Maturity**: 8/10 — genuinely the most polished UI in the system (a working kanban board with inline stage-change and conversion-rate metrics) — a real strength worth highlighting.
- **Workflow Maturity**: 5/10 — the pipeline-visualization half is excellent; the actual "conversion" step is currently cosmetic only — moving a lead to `Member` stage does **not** create a linked `Member` record anywhere in the code. This is a workflow-correctness bug (it *looks* like it worked), not merely a missing feature.
- **Testing Status**: 0/10.
- **Documentation Status**: 3/10.
- **Production Readiness**: 6/10 — otherwise strong module, meaningfully undermined by the silent-non-conversion bug.
- **Commercial Readiness**: 7/10 — close to demo-ready once the conversion-linking gap is fixed; among the highest-ROI near-term fixes in the whole inventory given how small the fix is relative to its visibility.
- **Dependencies**: Members (the conversion target).
- **Missing Pieces**: wire `UpdateLeadStageCommand`'s Member-stage transition to actually create/link a real `Member` record.

---

## 9. Trainer Management

- **Current Purpose**: Trainer roster, client assignment, scheduling, ratings, and commission tracking.
- **Backend Maturity**: 6/10 — roster creation (with real temp-password provisioning), client assignment, and rating commands all exist and are solid; trainer schedules and commission records have **no live command path at all** beyond initial demo seeding.
- **Frontend Maturity**: 6/10 — roster + client-assignment UI works well; no schedule-management UI exists; no rating UI exists despite the command being ready.
- **Workflow Maturity**: 3/10 — the weakest workflow chain in the system: a trainer can be hired and assigned clients, but their schedule can never be set by anyone through the product, they can never be rated by a member through the product, and their commission is never generated or marked paid by any code path whatsoever.
- **Testing Status**: 0/10.
- **Documentation Status**: 3/10.
- **Production Readiness**: 4/10 — a trainer literally cannot be paid correctly by this system today; a genuine business-risk-level gap for trainer retention, not a cosmetic one.
- **Commercial Readiness**: 4/10.
- **Dependencies**: Billing (commission should logically accrue from paid trainer-attributed invoice lines).
- **Missing Pieces**: schedule-management UI; rating UI; commission-generation logic tied to completed sessions/invoices; a payout/mark-paid action. *(Note: a basic commission-accrual-and-mark-paid workflow can reasonably be built without waiting on the full, deferred Payroll module — worth flagging as a partial exception to the broader deferral, since it closes a concrete trainer-trust gap using only modules that already exist.)*

---

## 10. Equipment

- **Current Purpose**: Physical asset registry and lifecycle-status tracking.
- **Backend Maturity**: 7/10 — asset and supplier commands both exist and are solid.
- **Frontend Maturity**: 6/10 — asset registry (create/list/status-change) works well; supplier *list* works, supplier *creation* has no dialog anywhere.
- **Workflow Maturity**: 6/10 — the asset half of the module is complete; the supplier half is half-built (view yes, create no).
- **Testing Status**: 0/10.
- **Documentation Status**: 3/10.
- **Production Readiness**: 6/10.
- **Commercial Readiness**: 6/10.
- **Dependencies**: Maintenance (work orders reference assets).
- **Missing Pieces**: a supplier-creation dialog.

---

## 11. Maintenance

- **Current Purpose**: Work-order tracking and preventive-maintenance scheduling for equipment.
- **Backend Maturity**: 7/10 — the work-order lifecycle (including correct automatic asset-status transitions and downtime-log open/close side effects) is solid; the recurring-schedule command exists, but nothing ever advances `NextDueDate` to auto-create the next work order.
- **Frontend Maturity**: 6/10 — work-order list/create/status-change UI works well; no schedule-management UI exists at all.
- **Workflow Maturity**: 5/10 — reactive (corrective) maintenance is a complete, correct workflow end to end; preventive maintenance is not — a schedule can be created but never fires automatically.
- **Testing Status**: 0/10.
- **Documentation Status**: 3/10.
- **Production Readiness**: 6/10.
- **Commercial Readiness**: 5/10.
- **Dependencies**: Equipment, background-job scheduling infrastructure (Hangfire is already in place and could host this).
- **Missing Pieces**: schedule-management UI; a background job that advances `NextDueDate` and auto-creates the next preventive work order.

---

## 12. Inventory

- **Current Purpose**: Stock-level tracking for retail/consumable items.
- **Backend Maturity**: 7/10 — a quick stock-adjust command and a separate, richer purchase-record command both exist, with some unreconciled overlap between them (both mutate `QuantityOnHand` and both write a `StockMovement` row via independent logic).
- **Frontend Maturity**: 6/10 — list + low-stock filtering + quick +/- adjust all work well; the purchase-record path has zero UI.
- **Workflow Maturity**: 5/10 — the quick-adjust workflow is solid for day-to-day corrections; there is no real purchase-order/reorder workflow, and the `low-stock` notification template that exists is never scheduled by anything.
- **Testing Status**: 0/10.
- **Documentation Status**: 3/10.
- **Production Readiness**: 6/10.
- **Commercial Readiness**: 5/10.
- **Dependencies**: Notifications (for low-stock alerting, once scheduled).
- **Missing Pieces**: a purchase-record UI; automated low-stock notification scheduling reusing the existing template; reconciliation of the two overlapping stock-adjustment code paths into one.

---

## 13. Workouts

- **Current Purpose**: Exercise library, workout-template building, and per-member workout logging.
- **Backend Maturity**: 7/10 — exercise, template, and logging commands all exist and are correct; logging in particular is **fully built server-side with zero frontend caller**.
- **Frontend Maturity**: 4/10 — the exercise library and template builder are genuinely well-built; logging has **no UI anywhere** — a complete, working backend feature with no way to exercise it (no pun intended) from the product.
- **Workflow Maturity**: 4/10 — a trainer can build an excellent workout template and then has no way, through the product, to ever record that a member actually performed it.
- **Testing Status**: 0/10.
- **Documentation Status**: 3/10.
- **Production Readiness**: 5/10.
- **Commercial Readiness**: 4/10 — one of the single highest-leverage near-term frontend investments in the entire codebase: the business logic is already done, so closing this gap is pure UI-building effort with no backend risk.
- **Dependencies**: Members.
- **Missing Pieces**: workout-logging UI (a staff-facing version is a reasonable near-term scope; a member-facing/mobile version is a larger, later investment per the gap analysis).

---

## 14. Nutrition

- **Current Purpose**: Food-item library and per-member diet-plan/meal/water-intake tracking.
- **Backend Maturity**: 7/10 — food-library, diet-plan, meal-entry, and water-log commands all exist and are correct.
- **Frontend Maturity**: 3/10 — only the food library has any UI at all; diet plans, meal entries, and water logging have **zero UI anywhere** despite being fully built server-side — the single largest backend-ahead-of-frontend gap in the entire system by feature count (3 of 4 sub-features are completely unreachable).
- **Workflow Maturity**: 3/10 — the same pattern as Workouts, more severe.
- **Testing Status**: 0/10.
- **Documentation Status**: 3/10.
- **Production Readiness**: 4/10.
- **Commercial Readiness**: 4/10 — the same "pure UI upside, zero backend risk" argument as Workouts, arguably stronger given the larger share of currently-wasted backend investment.
- **Dependencies**: Members.
- **Missing Pieces**: diet-plan/meal-entry/water-log UI (staff-facing at minimum).

---

## 15. Reports

- **Current Purpose**: Operational and financial reporting with genuine Excel export.
- **Backend Maturity**: 7/10 — 3 genuinely well-built aggregation queries (revenue, attendance, membership breakdown) with real ClosedXML `.xlsx` export; the other 4 "report" tabs have no dedicated backend query at all (they reuse existing list-endpoint data, aggregated client-side).
- **Frontend Maturity**: 7/10 — all 7 tabs render correctly; this was directly browser-verified earlier in this session, including confirming the export buttons return genuine binary files (not empty placeholders).
- **Workflow Maturity**: N/A — a reporting surface, not a transactional workflow.
- **Testing Status**: 0/10.
- **Documentation Status**: 3/10.
- **Production Readiness**: 7/10 — one of the two strongest "complete" modules in the system alongside CRM.
- **Commercial Readiness**: 6/10 — solid for a demo; would benefit from export capability on the 4 currently view-only tabs, and eventually from a genuine BI layer (explicitly a later-phase item per the gap analysis, not an in-scope gap here).
- **Dependencies**: Billing, Attendance, Members, Trainers, Inventory, Equipment, Maintenance (every module it reports on).
- **Missing Pieces**: dedicated backend queries + export for the 4 client-aggregated tabs (a lower-priority nicety, since they already function correctly as-is).

---

## 16. Notification Center

- **Current Purpose**: Notification-template management and an in-app "Dev Mailbox" standing in for real email/SMS/WhatsApp delivery.
- **Backend Maturity**: 7/10 — template CRUD, log viewing, scheduled-notification viewing, and a manual dispatch trigger all work correctly — this was directly built, browser-verified, and had a real bug fixed (unrendered `{{placeholder}}` tokens) earlier in this session.
- **Frontend Maturity**: 8/10 — all 3 tabs work well, browser-verified.
- **Workflow Maturity**: 5/10 — only 1 of 5 seeded notification categories (`MembershipExpiry`) is ever actually scheduled by any code path; `Maintenance`, `Birthday`, `FollowUp`, and `LowStock` templates exist with nothing that ever triggers them.
- **Testing Status**: 0/10.
- **Documentation Status**: 3/10.
- **Production Readiness**: 3/10 — real send capability is **[DEFERRED]** per user direction (this is precisely the "3rd-party service" category flagged for later); the *scheduling* gap for the 4 orphaned templates is independent of the send-provider question and remains in-scope.
- **Commercial Readiness**: 3/10 (deferred aside — the Dev Mailbox itself is genuinely demo-solid).
- **Dependencies**: Maintenance, CRM, Inventory (each currently-orphaned template category needs a trigger from its owning module). **[DEFERRED dependency: a real email/SMS/WhatsApp provider.]**
- **Missing Pieces (in-scope)**: scheduling logic for the 4 orphaned templates, following the exact pattern already established by `MembershipExpiryCheckJob`. **(Out of scope per direction: real send-provider integration.)**

---

## 17. Settings

- **Current Purpose**: Gym-profile, branch, and permission-matrix administration.
- **Backend Maturity**: 1/10 — exactly one read-only query exists (`GetBranchesQuery`, used only to populate branch-selector dropdowns elsewhere in the app).
- **Frontend Maturity**: 0/10 — no `modules/settings` folder exists in the frontend at all.
- **Workflow Maturity**: 0/10 — no administrative workflow exists in any form.
- **Testing Status**: 0/10.
- **Documentation Status**: 1/10.
- **Production Readiness**: 1/10 — **the single most consequential in-scope gap in the entire inventory**: without this module, basic tenant/branch/permission administration requires direct database intervention, which is incompatible with running this as a real product for even one paying customer.
- **Commercial Readiness**: 1/10.
- **Dependencies**: RBAC (the permission-matrix editor's data model already exists and is ready to be surfaced), Members/every branch-scoped module (branch data is referenced throughout the system and currently has no administration surface).
- **Missing Pieces**: essentially the entire module — gym-profile view/edit, branch CRUD, a permission-matrix editor, system-preference management, and the entire frontend module folder.

---

## 18. Migration Center

- **Current Purpose**: Bulk CSV/Excel import of Members/Trainers/Equipment/Inventory/Payments to accelerate customer onboarding.
- **Backend Maturity**: 0.5/10 — domain entities (`ImportJob`, `ImportRow`, `ImportFieldMapping` + 3 enums) exist; zero Application-layer commands, queries, or controller.
- **Frontend Maturity**: 0/10 — no `modules/migration` folder exists.
- **Workflow Maturity**: 0/10.
- **Testing Status**: 0/10.
- **Documentation Status**: 1/10.
- **Production Readiness**: 0/10.
- **Commercial Readiness**: 1/10 — correctly a lower near-term priority than Settings, since this is primarily a sales-enablement/onboarding accelerator rather than a day-1 operational necessity (a small number of manually-entered records is a viable, if tedious, workaround for a pilot customer; the absence of Settings is not similarly workaroundable).
- **Dependencies**: File storage (currently unwired — needed for CSV upload), background-job queue (Hangfire already exists and could host async import processing).
- **Missing Pieces**: the entire module.

---

## Summary: Production-Readiness Ranking

Ordered from strongest to weakest, to inform Phase 3 (Backend/Frontend Parity) and Phase 4 (Complete Existing Workflows) sequencing:

| Rank | Module | Production Readiness | Primary Blocker |
|---|---|---|---|
| 1 | Reports | 7/10 | Minor — 4 tabs lack dedicated export |
| 2 | CRM & Leads | 6/10 | Lead→Member conversion doesn't create a real Member |
| 3 | Members | 6/10 | No edit-profile / add-record UI |
| 4 | Equipment | 6/10 | No supplier-creation UI |
| 5 | Maintenance | 6/10 | No recurring-schedule automation |
| 6 | Dashboard | 6/10 | 4 hardcoded-zero KPIs, stale copy |
| 7 | Inventory | 6/10 | No purchase-record UI, overlapping stock paths |
| 8 | RBAC | 6/10 | 3 orphaned permission codes, no management UI |
| 9 | Attendance | 5/10 | No check-out UI |
| 10 | Memberships | 5/10 | Discounts/coupons write-only |
| 11 | Workouts | 5/10 | Logging fully backend-ready, zero UI |
| 12 | Authentication | 4/10 | MFA unreachable, tokens in localStorage |
| 13 | Trainers | 4/10 | Schedule/commission/rating all dead-ended |
| 14 | Nutrition | 4/10 | 3 of 4 sub-features fully backend-ready, zero UI |
| 15 | Billing | 3/10 | Real gateway deferred; refund UI missing regardless |
| 16 | Notification Center | 3/10 | Real send deferred; 4 templates never scheduled regardless |
| 17 | Settings | 1/10 | Essentially unbuilt — blocks basic administration |
| 18 | Migration Center | 0/10 | Unbuilt — lower priority than Settings |

**Foundation Inventory complete.** Proceeding to Phase 3 (Backend/Frontend Parity) is unblocked; this ranking should directly inform sequencing within that phase.
