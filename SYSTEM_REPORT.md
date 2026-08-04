# GymOS — System Report

**What this document is:** a complete, from-the-code explanation of what GymOS is today —
architecture, request lifecycle, database, every module's actual functionality, how the 8 user
roles relate to each other and to the data, and the assumptions the whole system was built under.
Every claim below was checked directly against the current source (entity files, permission
tables, seed data, `Program.cs`, controllers, a fresh test run) on 2026-08-04, not copied from an
earlier planning document. Where the checked-in `README.md` still uses "Wave 1 / Wave 2-3"
language from the original build plan, treat that as historical framing — all 16 modules listed
below are fully built end-to-end (backend + frontend), not scaffolded.

---

## 1. What GymOS is

GymOS is a **demo-ready, multi-module gym management platform** — one system covering
memberships, billing, attendance, CRM, staff/trainer management, equipment/maintenance,
inventory, workouts, nutrition, reporting, notifications, and a member self-service portal. It
was built as a **sales/demo tool first**: a prospective client should be able to log in as any of
8 roles and see a fully working product with realistic data, without GymOS needing real payment
credentials, a real email/SMS provider, or real client data to show it. Every external dependency
(payments, email/SMS/WhatsApp, file storage, QR/door hardware) sits behind an interface with a
working **demo implementation today**, swappable for a real one later via configuration — this
assumption shapes almost every architectural decision described below.

The system currently runs as a **single tenant** ("Titan Fitness", 3 branches) — multi-tenancy is
fully modeled in the schema and enforced at the ORM layer, but the UI never exposes a
tenant-switcher, because the product is being demoed to one prospective client at a time, not
sold as self-serve SaaS yet.

---

## 2. Technology stack

| Layer | Technology |
|---|---|
| Backend framework | ASP.NET Core (.NET 10) |
| Backend architecture | Clean Architecture (Domain → Application → Infrastructure → API), CQRS via MediatR |
| Database | PostgreSQL 16+, accessed via EF Core |
| Real-time | SignalR (2 hubs: notifications, dashboard) |
| Background jobs | Hangfire, PostgreSQL-backed storage, 8 daily/periodic recurring jobs |
| Auth | Custom JWT + refresh-token model (not ASP.NET Identity), permission-based authorization |
| Validation | FluentValidation, run as a pipeline behavior on every command |
| Frontend framework | React 18 + TypeScript, built with Vite |
| Frontend styling | Tailwind CSS v4 + shadcn/ui components |
| Frontend server state | TanStack Query (React Query) |
| Frontend client state | Zustand (auth session, selected branch, UI state) |
| Routing | React Router v6 (deliberately pinned below v7 — see README "Known environment notes") |

---

## 3. Architecture

### 3.1 Backend layers

```
GymOS.Shared          Result<T>, PagedList<T>, PermissionCodes, RoleNames — zero dependencies
     ↑
GymOS.Domain           Entities, enums, domain interfaces (ITenantScoped, IBranchScoped, IAuditable,
                        ISoftDelete). Zero references to any other layer.
     ↑
GymOS.Application      CQRS handlers (MediatR), one folder per module: Commands/ Queries/ Dtos/
                        Validators. Defines interfaces for every external dependency
                        (IPaymentGateway, IEmailSender, IObjectStorage, etc.) — never implements them.
     ↑
GymOS.Infrastructure    EF Core + PostgreSQL, JWT issuance, password hashing, the demo/no-op
                        implementations of every external interface, SignalR hub classes, Hangfire
                        job classes, the demo-data seeder.
     ↑
GymOS.API              Controllers (thin pass-throughs to MediatR), JWT bearer auth wiring,
                        permission-policy registration, Program.cs composition root.
```

This is verified as the actual dependency direction, not an intended one — `GymOS.Domain` has
zero `using` references to Application/Infrastructure/API, `GymOS.Application` has zero
references to Infrastructure/API, and the `.csproj` `ProjectReference` graph matches exactly.
Controllers contain no business logic beyond the standard "route id must match body id" REST
guard.

### 3.2 Why CQRS/MediatR

Every state change is a `Command`, every read is a `Query`, both dispatched through MediatR to a
single handler. This isn't just a style choice — it's what makes the **pipeline behaviors**
possible: six cross-cutting concerns run identically for every one of the ~130+ commands/queries
in the system, in this fixed order:

```
Request → TenantScopeBehavior → BranchScopeBehavior → LoggingBehavior → ValidationBehavior
        → TransactionBehavior → AuditBehavior → Handler → Response
```

1. **TenantScopeBehavior** — stamps/validates the current tenant on the request.
2. **BranchScopeBehavior** — same, for branch, where applicable.
3. **LoggingBehavior** — structured log of every request in/out.
4. **ValidationBehavior** — runs the matching FluentValidation validator (if one exists) and
   short-circuits with a 400 before the handler ever runs.
5. **TransactionBehavior** — wraps the handler in a DB transaction; a nested command that throws
   rolls back everything, including any audit entries already written in that request.
6. **AuditBehavior** — after a successful command, writes an `AuditLog` row (who, what, when,
   which entity) automatically. No handler opts into auditing manually — it's structural, not a
   per-feature checkbox.

### 3.3 Frontend structure

```
frontend/src/
  app/                  router.tsx (route table), providers (QueryClient, etc.)
  modules/<name>/        one folder per business module — api/ (React Query hooks), components/
                         (dialogs, cards), pages/ (route-level screens)
  shared/                AppShell, Sidebar/MobileNav/Topbar, NAV_MODULES (permission-gated nav),
                         StatCard, reusable hooks
  components/ui/          shadcn primitives (Button, Dialog, Table, Select, etc.)
  stores/                 Zustand: authStore (JWT + user + permissions), uiStore (selected branch)
```

18 module folders exist: the 17 sidebar-navigable modules (Dashboard, My Account/Portal, Members,
Memberships, Attendance, Billing, CRM, Trainers, Equipment, Maintenance, Inventory, Workouts,
Nutrition, Reports, Notification Center, Migration Center, Settings) plus `auth/` (login, forgot
password, account/security page — not itself a nav item). Every sidebar entry in `NAV_MODULES` is
tagged `wave: 1`, meaning nothing is hidden behind a "Coming soon" placeholder anymore — that flag
exists in the code for historical/extensibility reasons but currently gates nothing.

The UI never hand-rolls "does this role see X" — every nav entry declares the one permission code
it requires, and `Sidebar`/`MobileNav` filter against the JWT's decoded permission list. The same
mechanism backs every "Assign/Manage" button inside a page.

---

## 4. Request lifecycle (how a click becomes a database row)

1. **Frontend** — a React Query hook (e.g. `useCreateMember`) fires an Axios request via a shared
   `apiClient`, which attaches the JWT access token and, on a 401, transparently attempts one
   refresh-token exchange before retrying the original request.
2. **API** — the controller action requires `[Authorize]` plus `[RequirePermission("members.create")]`
   (a policy registered per permission code in `Program.cs`, one for every code in
   `PermissionCodes.All` — discovered by reflection, so a new permission constant is automatically
   a real ASP.NET Core authorization policy with no separate registration step).
3. **MediatR pipeline** — the six behaviors above run in order.
4. **Handler** — talks to `IApplicationDbContext` (EF Core), or to one of the injected interface
   abstractions (`IPaymentGateway`, `IEmailSender`, etc.) for anything outside the database.
5. **EF Core global query filters** — every tenant-scoped entity is automatically filtered to
   `TenantId == currentTenant` at the LINQ-provider level; a handler cannot accidentally query
   across tenants even if it forgets to filter manually. Branch-scoped entities get the same
   treatment for `BranchId`.
6. **Response** flows back through the same MediatR pipeline, gets serialized (enums as string
   names, e.g. `"Active"`, not raw integers — so the frontend's TypeScript union types line up
   with the JSON), and the frontend's React Query cache updates.

### 4.1 Real-time updates

Two SignalR hubs (`/hubs/notifications`, `/hubs/dashboard`) push live updates without a page
refresh — e.g. a front-desk check-in updates the owner's dashboard stat cards live. WebSocket
connections can't carry an `Authorization` header, so the JWT is passed via `?access_token=` query
string, accepted only for paths under `/hubs`.

### 4.2 Background jobs (Hangfire, dashboard at `/hangfire`)

Eight recurring jobs run daily (one every 5 minutes):

| Job | Cadence | Purpose |
|---|---|---|
| `membership-expiry-check` | daily | flags memberships expiring soon |
| `membership-expiry-transition` | daily | flips expired memberships to `Expired` status |
| `invoice-overdue-transition` | daily | flips unpaid past-due invoices to `Overdue` |
| `birthday-check` | daily | schedules birthday notifications |
| `maintenance-due-check` | daily | schedules maintenance-due notifications |
| `low-stock-check` | daily | schedules low-stock alerts (fires once per shortfall, resets on restock) |
| `follow-up-reminder-check` | daily | schedules CRM lead follow-up reminders |
| `notification-dispatch` | every 5 min | sends anything scheduled and due, via the demo `IEmailSender`/`ISmsSender`/`IWhatsAppSender` (logs to `NotificationLog`, visible in-app as the "Dev Mailbox") |

---

## 5. Multi-tenancy & branch model

- **Tenant** — the top-level customer of GymOS itself (a gym business). Exactly one exists today
  ("Titan Fitness"), but the schema and every query already scope to it.
- **Branch** — a physical location under a tenant. Three exist in the demo tenant. Almost every
  business entity (`Member`, `Invoice`, `AttendanceRecord`, `Asset`, etc.) carries a `BranchId`.
- **UserBranchAccess** — a join table controlling which branches a given staff `User` can operate
  in. Owner and Manager get access to all branches; every other role is scoped to just the first
  branch in the demo seed. The frontend's `BranchSwitcher` (top bar) lets a multi-branch user pick
  which branch's data they're viewing.
- Enforcement is **structural, not convention**: EF Core global query filters apply the
  tenant/branch scope automatically to every LINQ query against a scoped entity, and
  `TenantScopeBehavior`/`BranchScopeBehavior` validate it again at the MediatR pipeline level
  before a handler runs.

---

## 6. Database / data model

PostgreSQL, 15 EF Core migrations applied (from `InitialCreate` through `AddWorkoutAssignment`),
schema and code confirmed in sync (`dotnet ef migrations has-pending-model-changes` reports none).
Every entity inherits `BaseEntity` (a `Guid Id`, `CreatedAt`/`UpdatedAt`) and opts into
`ITenantScoped`/`IBranchScoped`/`IAuditable`/`ISoftDelete` as needed. Entities grouped by module:

| Module | Entities |
|---|---|
| **Tenancy** | `Tenant`, `Branch` |
| **Identity/RBAC** | `User`, `Role`, `Permission`, `RolePermission`, `UserRole`, `UserBranchAccess`, `RefreshToken`, `PasswordResetToken` |
| **Members** | `Member`, `EmergencyContact`, `MedicalNote`, `MemberMeasurement`, `ProgressPhoto`, `MemberMembership` |
| **Memberships** | `MembershipPlan`, `Discount`, `Coupon` |
| **Billing** | `Invoice`, `InvoiceLine`, `Payment`, `Refund`, `PaymentReminder` |
| **Attendance** | `AttendanceRecord` |
| **CRM** | `Lead`, `LeadActivity` |
| **Trainers** | `Trainer`, `TrainerAssignment`, `TrainerSchedule`, `TrainerSession`, `TrainerRating`, `CommissionRecord` |
| **Equipment** | `Asset`, `Supplier` |
| **Maintenance** | `WorkOrder`, `MaintenanceSchedule`, `DowntimeLog` |
| **Inventory** | `InventoryItem`, `StockMovement`, `PurchaseRecord` |
| **Workouts** | `Exercise`, `WorkoutTemplate`, `WorkoutTemplateExercise`, `WorkoutLog`, `WorkoutLogEntry`, `WorkoutAssignment` |
| **Nutrition** | `FoodItem`, `DietPlan`, `MealEntry`, `WaterLog` |
| **Notifications** | `NotificationTemplate`, `ScheduledNotification`, `NotificationLog` |
| **Migration Center** | `ImportJob`, `ImportRow`, `ImportFieldMapping` |
| **Settings** | `GymProfile`, `SystemPreference` |
| **Auditing** | `AuditLog` |

### 6.1 Notable relationships

- **`Member` is the customer record; `User` is a login identity.** They are linked by a
  *nullable* `Member.UserId` — most seeded members (300 of them) have no `User` row at all and
  cannot log in; only the demo "member@titanfitness.demo" account is deliberately linked. This is
  the exact mechanism the Member Portal depends on (see §8).
- **`Trainer.UserId` is required (not nullable)** — every Trainer record is a staff member with a
  real login, unlike `Member`.
- **`WorkoutAssignment`** references an existing `WorkoutTemplate` rather than duplicating its
  exercise list — a trainer-assigned plan is "this template, on this member, starting this date,"
  reusing the template's `WorkoutTemplateExercise` rows for the prescribed sets/reps.
- **`InvoiceLine.InventoryItemId`** (nullable) is what closes the point-of-sale loop: a product
  sale invoice line can reference an inventory item, and paying that invoice creates a
  `StockMovement` that decrements stock automatically.
- **`MemberMembership.InvoiceId`** links a membership purchase/renewal to the invoice that was
  generated for it in the same transaction — a renewal always produces a real invoice.

---

## 7. Authentication & authorization

- **Login** — email + password (bcrypt-hashed via a custom `IPasswordHasher`), issuing a
  short-lived JWT access token plus a longer-lived, rotating, hashed, revocable refresh token.
  Five wrong passwords lock the account for 15 minutes (tracked in-process, deliberately outside
  the request's own DB transaction so the lockout counter survives a rolled-back attempt).
- **JWT contents** — `sub` (user id), `tenant_id`, `email`, and role name(s) only. **Permissions
  are never embedded in the token** — they're resolved server-side per request (and cached), so
  revoking a permission takes effect immediately without waiting for a token to expire.
- **MFA** — real TOTP (time-based one-time password), opt-in per user, no third-party dependency.
- **Authorization** — one ASP.NET Core policy per permission code (37 codes today, listed in
  §9.1), enforced via `[RequirePermission("code")]` on controller actions. There is no
  role-name-based authorization anywhere in the codebase — every check is a permission code, and
  roles are just named bundles of permission codes assigned at seed time (or editable later via
  the in-app Permission Matrix, see §10.16).

---

## 8. The Member Portal — a structurally separate surface

This is the one place in the system where "view" access is **not** staff-wide by design. Early in
this project, `Attendance.View`/`Workouts.View`/`Nutrition.View`/`Dashboard.View` were briefly
also granted to the Member role, which meant a member could read *any* member's attendance,
workouts, or nutrition just by supplying an id — those staff-facing endpoints trust a
caller-supplied `memberId` and only check "does this role have View," never "is this your own
record." That was fixed by:

- Giving the `Member` role **only** `Portal.View`, nothing else.
- Building a separate `/api/me/*` surface (`GetMyProfileQuery`, `GetMyAttendanceQuery`,
  `GetMyWorkoutLogsQuery`, `GetMyWorkoutAssignmentsQuery`, `GetMyDietPlansQuery`,
  `GetMyWaterLogsQuery`) where **no query accepts a memberId parameter at all** — "whose data" is
  resolved server-side, every time, via `MyMemberResolver`: JWT → `ICurrentUserService.UserId` →
  `Member.UserId` → `Member.Id`. A `memberId` smuggled onto the query string is structurally
  impossible to use, because there's no parameter to bind it to.
- A portal-linked user whose `Member.UserId` link doesn't exist gets a 404, not another member's
  data.

This is covered by integration tests proving the smuggling case is ignored and cross-member access
is blocked, and the frontend renders an entirely different page (`MemberPortalPage`, card-based,
no data table) for the Member role instead of the staff dashboard.

---

## 9. User roles & how they relate to each other

Eight roles exist, seeded with one demo login each (`{role}@titanfitness.demo`, password
`Demo@12345` for all). Roles are **not hardcoded checks** — they're rows in the `Role` table, each
holding a set of `RolePermission` grants that can be edited live from Settings → Permission
Matrix by anyone holding `settings.manage_permissions` (Owner only, by default).

### 9.1 Role → permission grants (as seeded)

| Role | Branch access | Permissions granted |
|---|---|---|
| **Owner** | all branches | every permission that exists (37/37) |
| **Manager** | all branches | every permission except `settings.manage_permissions` |
| **Receptionist** | first branch | Dashboard.View, Members (View/Create/Update/ManageMembership), Memberships.View, Billing (View/CreateInvoice/RecordPayment), Attendance (View/CheckIn), CRM (View/ManageLeads) |
| **Trainer** | first branch | Dashboard.View, Members.View, Trainers.View, Attendance.View, Workouts (View/Manage) |
| **Nutritionist** | first branch | Dashboard.View, Members.View, Nutrition (View/Manage) |
| **Accountant** | first branch | Dashboard.View, Billing (View/CreateInvoice/RecordPayment/IssueRefund), Memberships.View, Reports.View |
| **Maintenance** | first branch | Dashboard.View, Maintenance (View/Manage), Equipment.View |
| **Member** | — (no staff branch access) | **Portal.View only** |

This table is not a description of intent — it is the literal `rolePermissionMap` seeded in
`DemoDataSeeder.cs`, and it's exactly what drives which sidebar items each demo login sees
(verified live: the Receptionist login shows precisely 6 modules — Dashboard, Members,
Memberships, Attendance, Billing, CRM — versus Owner's full 16).

### 9.2 How the roles relate to each other through the data (not just permissions)

Permissions describe *what a role can see*; the entities below describe *how a Member's journey
actually touches multiple staff roles*:

- **Receptionist ↔ Member**: registers the `Member`, sells/renews their `MembershipPlan` (via
  `MemberMembership`), checks them in (`AttendanceRecord`), and creates/collects their `Invoice`.
- **Accountant ↔ Member**: records payments and issues refunds against the same invoices the
  Receptionist created — a separation of "who can create a charge" from "who can approve money
  back."
- **CRM (Receptionist role today) ↔ Member**: a `Lead` moves through
  Lead → FollowUp → Trial → Member stages; reaching the `Member` stage creates a real `Member` row
  via a nested command — the lead pipeline and the membership system are the same data, not two
  systems bridged by hand.
- **Trainer ↔ Member**: `TrainerAssignment` links a `Trainer` to a `Member` as an active client;
  `TrainerSchedule`/`TrainerSession` book and track PT sessions (only a `Scheduled` session can be
  completed — a real state machine, not a status field anyone can set); `TrainerRating` lets a
  member-facing rating roll up to the trainer's average; `CommissionRecord` tracks what the gym
  owes the trainer per period.
- **Trainer ↔ Workouts ↔ Member**: a Trainer (or anyone with `workouts.manage`) builds a
  `WorkoutTemplate` (a reusable exercise list), then creates a `WorkoutAssignment` putting that
  template on a specific `Member`'s plan — the member sees exactly this in their own portal
  (§8), fixing what was originally a real gap: members could see an assigned diet plan but not an
  assigned workout plan, until `WorkoutAssignment` was added to mirror `DietPlan`'s pattern.
- **Nutritionist ↔ Member**: creates a `DietPlan` (name + target calories) for a member; the
  member (or staff) logs `MealEntry`/`WaterLog` rows against it day to day.
- **Maintenance ↔ Equipment**: `WorkOrder`s reference an `Asset`; approving a schedule-linked work
  order restores the asset to service, closes the `DowntimeLog`, and advances the
  `MaintenanceSchedule`'s next-due date — one transaction, not three separate manual steps.
- **Everyone ↔ AuditLog**: every command any role successfully executes writes an audit row
  automatically (§4.2's `AuditBehavior`) — Owner/Manager can review "who did what, when" across
  every other role from Settings → Audit Log.

### 9.3 The Member's own view vs. everyone else's view of them

A `Member` business record is touched by up to six different staff roles (Receptionist,
Accountant, Trainer, Nutritionist, Maintenance indirectly via shared equipment, Owner/Manager for
oversight) — but the Member's own logged-in view (§8) only ever shows *their own* slice: profile,
membership status, attendance history, assigned workout plan(s), assigned diet plan(s), and water
log. They cannot see other members, staff-only figures (revenue, other members' data), or
anything outside `/api/me/*`.

---

## 10. Feature inventory, module by module

Every module below has a working backend (entity → command/query → controller, permission-gated)
and a working frontend page reachable from the sidebar for any role holding the relevant
permission.

1. **Dashboard** — 10 live stat cards (today's revenue, cash collected, active members, new
   members this month, expiring-in-7-days, today's check-ins, trainers scheduled today, equipment
   alerts, maintenance reminders, inventory alerts), updates live via SignalR. Redirects any user
   without `dashboard.view` to their correct landing page instead of a stuck loading state (fixed
   2026-08-04).
2. **My Account / Member Portal** — the Member-role self-service view described in §8, plus a
   universal `/account` page (change password, enable 2FA) available to every role.
3. **Members** — full CRUD, QR code display per member, emergency contacts, medical notes,
   measurements over time, progress photos, membership history, branch transfer.
4. **Memberships** — plan catalog (6 seeded plans: Monthly/Quarterly/Annual/Family/
   Corporate/Custom), discounts and coupons with redemption caps and validity windows,
   freeze/resume (a middle path instead of cancellation, with plan-level freeze-day limits),
   renewal (always produces an invoice in the same transaction).
5. **Attendance** — simulated QR / manual check-in and check-out, peak-hours chart (last 30
   days), full history.
6. **Billing & Invoicing** — invoice creation, line items (including inventory-linked product
   sales), payment recording, refunds, overdue auto-transition, receipt-style detail view.
7. **CRM & Leads** — Kanban-style pipeline (Lead → FollowUp → Trial → Member → Lost), activity
   logging, conversion-rate reporting, CSV import.
8. **Trainers** — roster, specialties, commission rate, client assignment, session
   scheduling/completion state machine, ratings, commission tracking per period.
9. **Equipment** — asset registry with status (Active/UnderMaintenance/OutOfService/Retired),
   supplier management, warranty tracking, sequential per-tenant asset tags.
10. **Maintenance** — work orders (Preventive/Corrective, priority levels, approval workflow with
    verification), recurring maintenance schedules, downtime logging.
11. **Inventory** — stock levels, low-stock alerts (fire once per shortfall, reset on restock),
    purchase records, manual stock adjustments, and the point-of-sale loop (an invoice line sale
    decrements stock via `StockMovement`).
12. **Workouts** — exercise library, reusable workout templates (exercises + sets/reps),
    self-/staff-logged workout history, and trainer-assigned plans (`WorkoutAssignment`, the
    newest feature, added 2026-08-04) visible in the member's own portal.
13. **Nutrition** — food item library (with macros), diet plans with target calories, meal
    logging, water intake logging.
14. **Reports** — 10 tabs (Revenue, Attendance, Membership, Trainers, Inventory, Equipment,
    Maintenance, CRM, Workouts, Nutrition), each with a chart and an Excel export.
15. **Notification Center** — templated notifications, a "Dev Mailbox" (in-app log of every
    notification that would have been sent), a scheduled-notifications queue, and a manual "run
    checks now" trigger for demo purposes.
16. **Migration Center** — generic CSV import pipeline (Upload → field-mapping → Validate →
    Preview → Commit → Rollback). **Only 5 of the 8 entity types declared in the `ImportEntityType`
    enum have a working handler today: Member, Trainer, Equipment, Inventory, and Lead.**
    `Membership`, `Attendance`, and `Payment` are declared in the enum but have no registered
    `IImportEntityHandler` — they are not actually importable through the UI yet, a real,
    previously-undocumented gap worth flagging.
17. **Settings** — gym profile, branch management, the live-editable Permission Matrix (grant/
    revoke a permission per role, verified idempotent), system preferences, and the audit log
    viewer.

---

## 11. Deferred / simulated integrations, and why

Every external dependency is an interface in `GymOS.Application`, implemented by a **demo/no-op**
class in `GymOS.Infrastructure`, registered in `DependencyInjection.cs`. Swapping to a real
provider is a registration change plus `appsettings` config — no handler, controller, or frontend
code changes.

| Interface | Demo implementation today | Real implementation later |
|---|---|---|
| `IPaymentGateway` | `NoOpPaymentGateway` — simulates authorize/capture/refund with a deterministic fake transaction id | Stripe / Mollie / SEPA Direct Debit |
| `IEmailSender` / `ISmsSender` / `IWhatsAppSender` | Log to `NotificationLog`, visible in-app as the "Dev Mailbox" | SendGrid/SMTP, Twilio, WhatsApp Business API |
| `IObjectStorage` | `LocalDiskObjectStorage` (dev) or `S3ObjectStorage` (already talks to any real S3-compatible endpoint if configured) | just point at a real bucket + credentials |
| Door/QR/biometric check-in | not built — Attendance uses a "select member → Check In" simulated flow | real door controller / RFID / biometric SDK |

This is a deliberate scope boundary, not an oversight — see §12 for what's explicitly still
required before this system could process real payments or real client data.

---

## 12. Assumptions, known limitations, and honest gaps

Stated plainly, because a "full-fledged report" should not oversell:

1. **No real integrations yet.** Payment, email/SMS/WhatsApp, and door/QR hardware are demo
   implementations by design (§11) — not a missing feature, a conscious "prove the architecture,
   swap the plumbing later" decision.
2. **Ops hardening is incomplete.** No backup/restore runbook, no production monitoring/alerting
   exists yet. Login brute-force lockout **is** in place (§7).
3. **Medical notes are stored unencrypted** at the column level — flagged since the original
   product spec as a pre-real-data compliance requirement, not yet addressed.
4. **Migration Center's entity coverage is incomplete** — 5 of 8 declared import types actually
   work (§10.16); `Membership`, `Attendance`, and `Payment` import are not implemented.
5. **No automated frontend test suite.** `frontend/package.json` has no `test` script — frontend
   correctness today relies on TypeScript's type checker (`tsc -b` as part of every `npm run
   build`) plus manual/live browser verification, not automated unit or e2e tests. The backend, by
   contrast, has 77 automated tests (14 Domain, 49 Application, 14 API integration — all passing
   as of this report) covering tenant isolation, the auth/MFA flow, permission enforcement, and
   real business-rule edge cases per module (e.g. a trainer session state machine, sequential
   per-tenant asset tags, idempotent permission grants).
6. **Single-tenant deployment today.** The schema and query-filter enforcement are fully
   multi-tenant-capable, but there is no tenant-switcher UI and no self-serve tenant signup — this
   is architected to *become* SaaS, not already operating as SaaS.
7. **`AuditLog.DataBefore` is always null** — a placeholder column, not wired up. Cheap to fix,
   not currently blocking anything.
8. **A handful of hard deletes exist** (role-permission revocation, inventory-import rollback) —
   reviewed and judged acceptable rather than switched to soft-delete, since neither represents
   customer-facing data loss.
9. **Demo data is deterministic and regenerable, not a production dataset.** Seeding is a CLI flag
   (`dotnet run -- --seed`), idempotent (no-op if a tenant already exists), producing one tenant,
   3 branches, all 8 roles/logins, 301 members, ~500 attendance records, 105 invoices, 20
   trainers, 80 equipment assets, 100 inventory items, 50 CRM leads, 30 maintenance work orders —
   all dates relative to "today" at seed time, so dashboard widgets like "expiring this week" are
   always populated regardless of when the demo is run.

---

## 13. Local development & deployment

Covered in full in `README.md`; summarized here for completeness:

- **Prerequisites**: .NET 10 SDK, Node 20+, PostgreSQL 16+, the `dotnet-ef` global tool.
- **Backend**: create the `gymos_dev` database, copy
  `appsettings.Development.json.example` → `appsettings.Development.json` and set a real
  `Jwt:SigningKey`, apply migrations (`dotnet ef database update`), seed demo data, `dotnet run`.
  Swagger UI at `/swagger` (Development only), Hangfire dashboard at `/hangfire`.
- **Frontend**: `npm install`, copy `.env.example` → `.env`, `npm run dev` (Vite dev server on
  `:5173`).
- **Production**: the checked-in `appsettings.json` ships an intentionally-placeholder JWT signing
  key; `Program.cs` **refuses to start** in the `Production` environment if that placeholder is
  still in use, forcing `Jwt__SigningKey` and `ConnectionStrings__GymOsDb` to be set via real
  environment variables (verified live in both the failing and succeeding case). Migrations apply
  the same way against any target connection string.

---

## 14. Where this goes next

Per the project's own phased execution guide, the system has formally exited "Foundation Phase" —
all 16 modules are workflow-complete, the architecture is stable and re-verified, and production
deployment has been demonstrated end-to-end (not just described). What comes next (Phase 13) is
explicitly **not scoped by this report**: it names six large, independent initiatives (Operational
Excellence, Member Experience Engine, Coaching Engine, Engagement Engine, AI Platform, Advanced
Analytics) as a multi-month roadmap, and picking a starting point among them is a product-priority
decision, not an architecture question this document can answer.

---

*Sources checked directly for this report: `README.md`, `PHASE9_COMMERCIAL_READINESS.md`,
`PHASE11_FOUNDATION_EXIT_AUDIT.md`, `PHASE12_ARCHITECTURE_FREEZE_REVIEW.md`, every file under
`backend/src/GymOS.Domain/`, `PermissionCodes.cs`, `RoleNames.cs`, `DemoDataSeeder.cs`,
`Program.cs`, `frontend/src/shared/nav/modules.ts`, `frontend/src/app/router.tsx`, and a fresh
`dotnet test` run against the current codebase (77/77 passing).*
