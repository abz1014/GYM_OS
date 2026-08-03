# Phase 11 — Foundation Exit Audit

> Per GYMOS_NEXT_PHASE_EXECUTION_GUIDE.md: every module must pass all 13 criteria below, or the
> whole project stays in Foundation Phase. This is not a self-report — every verdict here is
> either a fresh check performed during this phase or a specific re-verified finding from a
> named earlier phase (cited inline). Where a check hasn't been done exhaustively, that is
> stated explicitly rather than implied.

## Overall verdict: **NOT READY TO EXIT FOUNDATION PHASE**

Three criteria fail for a material number of modules. Per the guide's own rule, that keeps the
whole project in Foundation Phase, not just the failing modules.

## Cross-cutting criteria (checked once, apply to all modules)

| Criterion | Verdict | Evidence |
|---|---|---|
| **Backend complete** | Pass | 126 permission-gated API actions across 18 controllers, all thin `ISender` pass-throughs (Phase 7). |
| **Validation complete** | Pass | FluentValidation present on every command that has fields to validate (Phase 6 sweep). |
| **Permissions complete** | Pass | Every action carries `[RequirePermission]` or documented `[AllowAnonymous]`/`[Authorize]`; live-verified 401/403/200 boundaries (Phase 6-8, re-confirmed this phase at 126 actions after adding the Portal surface). |
| **Audit complete** | Pass | Every `ICommand` audited inside its transaction; the anonymous-auth gap (Phase 5) and the redaction-destroying-business-data bug (Phase 10, `CouponCode`/`MemberCode`/`TemplateCode`) are both fixed and tested. |
| **Notifications complete** | Pass | 8 recurring jobs, dedup logic re-verified live this session (forced a real low-stock event, confirmed 3 notifications, confirmed dedup flag), Dev Mailbox for anonymous flows. |
| **Security complete** | **Conditional fail — see below** | |

### Security complete — the one criterion that needs an asterisk

A user report during this phase surfaced a real, live-exploitable cross-member data exposure:
the Member role (a gym customer, not staff) was seeded with staff-wide `dashboard.view`,
`attendance.view`, `workouts.view`, `nutrition.view` permissions, so a member's login could read
the executive dashboard's revenue figures, all 500 attendance records with every member's name,
and any other member's workout/diet/water logs by supplying their id. This is now fixed — a
`Portal.View` permission and an `/api/me/*` surface that resolves "whose data" server-side from
the JWT and accepts no id parameter at all, structurally (not by convention) — and covered by 4
new regression tests including a direct id-smuggling attempt.

**What this means for "Security complete" as a Phase 11 gate**: the fix closes the specific
pattern found. It was found because a human looked at a screenshot and asked a question, not
because of a systematic security audit of every endpoint. I have not performed a full
penetration-style sweep of all 126 actions for similar issues (e.g., can a Trainer with
`trainers.manage` modify another trainer's commission records; can a Receptionist see medical
notes for members outside their assigned branch). Those are staff-to-staff boundaries, lower
severity than customer-to-customer exposure, but unverified. **Recommend a dedicated security
review pass before this gate is called Pass**, not folded into a phase that was already scoped
to something else.

## Per-module criteria (vary by module)

Legend: ✅ Pass · ⚠️ Partial · ❌ Fail

| Module | Workflow | Reporting | Import/Export | Tests | Responsive UI |
|---|---|---|---|---|---|
| Dashboard | ✅ | — (is a report) | — | ⚠️ no dedicated tests | ✅ verified |
| Members | ✅ | ✅ | ✅ import+export | ⚠️ indirect only | ✅ fixed this phase (tab wrap) |
| Memberships | ✅ Phase 4-verified | ✅ | — | ⚠️ indirect only | ✅ fixed this phase |
| Attendance | ✅ | ✅ | — | ⚠️ indirect only | not spot-checked |
| Billing | ✅ | ✅ | — | ⚠️ indirect only | ✅ verified this phase |
| CRM & Leads | ✅ | ✅ | ❌ no lead import | ❌ zero dedicated tests | ✅ fixed this phase |
| Trainers | ✅ Phase 4-verified | ✅ | ✅ | ❌ zero dedicated tests | ✅ fixed this phase |
| Equipment | ✅ | ✅ | ✅ | ❌ zero dedicated tests | not spot-checked |
| Maintenance | ✅ Phase 4-verified | ✅ | — | ❌ zero dedicated tests | ✅ fixed this phase |
| Inventory | ✅ | ✅ | ✅ | ⚠️ domain logic only | not spot-checked |
| Workouts | ✅ functional | ❌ no aggregate report | ❌ no import/export | ❌ zero dedicated tests | ✅ fixed this phase |
| Nutrition | ✅ functional | ❌ no aggregate report | ❌ no import/export | ❌ zero dedicated tests | ✅ fixed this phase |
| Reports | ✅ | is the surface | ✅ export everywhere | ⚠️ permission tests only | ✅ Phase 8 |
| Notifications | ✅ | — | — | ❌ zero dedicated tests | ✅ fixed this phase |
| Migration Center | ✅ | — | is the surface | ❌ zero dedicated tests | not spot-checked |
| Settings | ✅ | — | — | ❌ zero dedicated tests | ✅ fixed this phase |
| **Portal (new)** | ✅ | — (self-service) | — | ✅ 4 security tests | ✅ verified live |

### Failing criteria, stated plainly

1. **Reporting / Import-Export — Workouts and Nutrition** (carried over from Phase 8, still
   unresolved): no aggregate report, no export, no import for either module. Everything else
   about them works — this is specifically the operational-completeness bar.
2. **Import — CRM**: Migration Center covers Members, Trainers, Equipment, Inventory. A gym
   migrating from another CRM has no lead-import path.
3. **Tests — 9 of 16 modules have zero dedicated test files**: CRM, Trainers, Equipment,
   Maintenance, Workouts, Nutrition, Notifications, Migration, Settings. The 44 automated tests
   that exist are real and valuable but concentrated on the highest-risk shared infrastructure
   (auth, tenant isolation, the renewal→invoice transaction, permission enforcement, the audit
   writer, now portal security) — not per-module coverage. A regression in, say, work-order
   status transitions or the CSV import field-mapping logic would not be caught by anything
   automated today; it would only surface in manual/live testing, the way this session has been
   catching bugs.

### Responsive UI — corrected this phase, but not exhaustively re-verified

Phase 8 fixed the missing mobile navigation (hamburger + drawer) but only spot-checked 2-3 pages.
This phase found a second, systemic bug while spot-checking further: **8 more pages** (Maintenance,
Member detail, Memberships, Notifications, Nutrition, Settings, Trainer detail, Workouts) used a
bare `<TabsList>` with no wrap behavior, causing the tab row to force the page into horizontal
scroll at 375px — found concretely on the Settings page (5 tabs, only 3 fit, no visible way to
reach the rest without knowing to scroll). Fixed uniformly (`h-auto flex-wrap`, matching the
pattern already applied to Reports) and re-verified live on Settings. **Not yet spot-checked**:
Attendance, Equipment, Inventory, Migration Center — these don't use `TabsList` so the specific
bug doesn't apply, but their table/form layouts haven't been individually confirmed at mobile
width the way Billing and Settings were this phase.

## What closing this gate actually requires

1. Build Workouts/Nutrition aggregate reporting + export (mirrors the Phase 6 pattern used for
   Trainer/Equipment/Inventory/CRM reports) and decide whether a lead-import handler belongs in
   Migration Center.
2. Write dedicated test coverage for the 9 untested modules — at minimum one handler test per
   module proving its core business rule (e.g., work-order status transitions, CSV field-mapping
   validation, commission calculation) rather than relying on manual verification alone.
3. Run a dedicated security review pass (not a side effect of a UI bug report) covering
   staff-to-staff boundaries, not just the customer-facing one already fixed.
4. Finish the mobile spot-check on the remaining unverified pages.

None of this invalidates the work done in Phases 1-10 and this phase — the architecture,
tenant isolation, audit trail, and the 14 modules that do pass are genuinely solid. This is an
honest count of what's left, which is exactly what a Foundation Exit Audit is for.
