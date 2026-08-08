# GymOS MVP

A demo-ready gym management platform built to show the complete vision of GymOS
without needing any real client data, credentials, or third-party integrations.
See [GymOS MVP Specification](docs/GymOS_MVP_Specification.md) for the original
product spec this MVP was built against.

Wave 1 (built): Auth/RBAC, Executive Dashboard, Member Management, Membership
Management, Attendance, Billing & Invoicing — fully working end to end.
Waves 2–3 (CRM, Trainers, Equipment, Maintenance, Inventory, Workouts,
Nutrition, Reports, Notification Center, Settings, Migration Center) have their
full database schema scaffolded and demo data seeded, with UI/API to follow in
later passes. The sidebar shows all 16 modules today — Wave 2/3 items are
marked "Coming soon."

## Repo layout

```
/backend    ASP.NET Core solution (Clean Architecture: Domain/Application/Infrastructure/API)
/frontend   React + TypeScript + Tailwind CSS + shadcn/ui
/docs       architecture notes
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (stable channel recommended — this repo was built against a preview 10.0.4xx SDK; if you hit SDK-related build issues, install the latest stable 10.0.x SDK)
- [Node.js 20+](https://nodejs.org/) and npm
- [PostgreSQL 16+](https://www.postgresql.org/download/) installed and running locally
- The [`dotnet-ef` global tool](https://learn.microsoft.com/en-us/ef/core/cli/dotnet): `dotnet tool install --global dotnet-ef`

## Backend setup

### 1. Create the database

Using `psql` (or any Postgres client) with your local superuser:

```sql
CREATE DATABASE gymos_dev;
```

### 2. Configure connection string and secrets

Copy the example dev settings file and fill in your own values:

```bash
cp backend/src/GymOS.API/appsettings.Development.json.example backend/src/GymOS.API/appsettings.Development.json
```

Edit `backend/src/GymOS.API/appsettings.Development.json` and set:
- `Jwt:SigningKey` — any long random string (e.g. `openssl rand -base64 48`)

Edit `backend/src/GymOS.API/appsettings.json` if your Postgres connection details differ from the default:
```json
"ConnectionStrings": {
  "GymOsDb": "Host=localhost;Port=5432;Database=gymos_dev;Username=postgres;Password=YOUR_PASSWORD"
}
```

### 3. Apply migrations

```bash
cd backend
dotnet ef database update --project src/GymOS.Infrastructure --startup-project src/GymOS.API
```

### 4. Seed demo data

```bash
dotnet run --project src/GymOS.API -- --seed
```

This seeds one tenant ("Titan Fitness"), 3 branches, all 8 roles with a
sensible permission matrix, one demo login per role, 300 members, 500
attendance records, 100 invoices/payments, plus Wave 2/3 volumes (20 trainers,
80 equipment assets, 100 inventory items, 50 CRM leads, 30 maintenance work
orders). All dates are relative to "today" at seed time, so dashboard widgets
like "expiring this week" are always populated. Seeding is idempotent — it's a
no-op if a tenant already exists (drop and recreate the database to reseed
from scratch).

### 5. Run the API

```bash
dotnet run --project src/GymOS.API
```

Swagger UI: `https://localhost:5001/swagger` (or check the console output for
the actual bound port/protocol — `dotnet run` prints it on startup). A
Hangfire dashboard for background jobs (membership expiry checks, notification
dispatch) is available at `/hangfire`.

**Demo logins** (all use password `Demo@12345`):
`owner@titanfitness.demo`, `manager@…`, `receptionist@…`, `trainer@…`,
`nutritionist@…`, `accountant@…`, `maintenance@…`, `member@…`, `member2@…`.

There are two member logins because the home screen proposes the next session
differently depending on whether a trainer has written the member a programme,
and only around a fifth of members have one. `member@` is self-directed — the
app offers their last session back. `member2@` is coached — the app offers the
trainer's plan, pre-filled with the loads that member last used. Signing in as
each shows both halves of the design.

### 6. Run the backend test suite

```bash
cd backend
./run-tests.sh
```

Don't run `dotnet test | tail -N` by hand — see "Known environment notes" below
for why that pattern can silently hide a failed build. `run-tests.sh` stops
any API server left running on port 5000 (a common self-inflicted build-lock),
builds first and aborts loudly on any error, then runs the suite and verifies
every test project under `tests/` actually reported a result before declaring
success.

## Frontend setup

```bash
cd frontend
npm install
cp .env.example .env   # set VITE_API_BASE_URL if the API isn't on http://localhost:5000
npm run dev
```

Open `http://localhost:5173` and log in with any demo account above.

## Production Deployment

The checked-in `appsettings.json` is for local development only — its JWT
signing key is a placeholder (literally named `CHANGE_ME_...`) and its DB
password won't match a real database. ASP.NET Core's standard configuration
precedence lets environment variables override any `appsettings.json` value
without editing the file, using `Section__Key` (double underscore) naming:

```bash
export ASPNETCORE_ENVIRONMENT=Production
export Jwt__SigningKey="<a real random secret, e.g. openssl rand -base64 48>"
export ConnectionStrings__GymOsDb="Host=<prod-host>;Port=5432;Database=<prod-db>;Username=<user>;Password=<real-password>"
export Cors__AllowedOrigins__0="https://<your-frontend-domain>"
```

**The API refuses to start** outside Development/Testing if either of two
things is wrong — both deliberate fail-fast guards in `Program.cs`:

| Guard | Fires when | Why it exists |
|---|---|---|
| `Jwt__SigningKey` | still the checked-in `CHANGE_ME_...` placeholder | Otherwise the deploy silently signs real user tokens with a key that is visible in public source control. |
| `Cors__AllowedOrigins` | empty, or every entry is localhost | An unset list does **not** open the API up — `WithOrigins([])` allows nobody — it makes the deployed frontend silently unable to call its own backend. Every request fails in the browser with nothing wrong in the API logs, which is an afternoon spent debugging the wrong layer. |

The environment check is an allow-list (`Development` or `Testing`), not a
`== Production` test, so a host named `Staging` or `Preview` is guarded too.

`ConnectionStrings__GymOsDb` has no equivalent guard — a bad value just fails
to connect, which is self-evident — but it must be overridden too. Note the
name: the API reads `ConnectionStrings__GymOsDb`, **not** the
`GYMOS_DB_CONNECTION` variable that `backend/run-api.sh` uses for local work.

Other settings that follow the same override pattern once a real integration
is ready (`Storage__Provider`, `Storage__*` for S3, or swapping the
`NoOpPaymentGateway`/`NoOpEmailSender`/etc. registrations in
`GymOS.Infrastructure/DependencyInjection.cs` for real ones) — see
"Deferred integrations" below.

Before first request, apply migrations against the target database:

```bash
cd backend
dotnet ef database update --project src/GymOS.Infrastructure --startup-project src/GymOS.API \
  --connection "Host=<prod-host>;Port=5432;Database=<prod-db>;Username=<user>;Password=<real-password>"
```

Verified this way end-to-end: with all variables set, the API starts with
`Hosting environment: Production`, `/health` returns 200, Swagger UI is
unreachable (404, gated by `app.Environment.IsDevelopment()`), login/JWT
issuance works normally, and a preflight from the configured origin comes back
with `Access-Control-Allow-Origin` while one from any other origin comes back
without it (so the browser blocks it). Both refusal guards were confirmed to
fire, including on `Staging`.

### Hosting shape

The frontend is a static Vite bundle and deploys anywhere — Vercel, Netlify,
any CDN. Point it at the API with `VITE_API_BASE_URL` at build time.

The backend **cannot** run on Vercel or any serverless platform, for two
reasons that are structural rather than incidental: it hosts three SignalR
hubs, which need persistent WebSocket connections, and fifteen recurring
Hangfire jobs (recurring billing, invoice overdue transitions, class session
generation, retention), which need a process that stays alive between
requests. It needs a container or an always-on app service — Railway, Render,
Fly.io, or Azure App Service.

**What this does not cover** — a real deploy still needs a backup/restore
runbook and production monitoring/alerting, neither of which exist yet
(tracked in `PHASE9_COMMERCIAL_READINESS.md`'s gap list, not a Foundation-exit
blocker per `PHASE12_ARCHITECTURE_FREEZE_REVIEW.md`).

## Architecture

- **Backend**: Clean Architecture — `GymOS.Domain` (entities, zero deps) →
  `GymOS.Application` (CQRS via MediatR, FluentValidation, interfaces for
  every external dependency) → `GymOS.Infrastructure` (EF Core/PostgreSQL, JWT,
  demo payment gateway, demo notification senders, S3-compatible storage,
  SignalR, Hangfire) → `GymOS.API` (controllers, JWT bearer auth,
  permission-based authorization).
- **Multi-tenancy**: single database, `TenantId`/`BranchId` discriminator
  columns, enforced via EF Core global query filters. Tenant is invisible in
  the UI — this is a single-client MVP architected so real SaaS multi-tenancy
  is a config change away, not a rewrite.
- **Deferred integrations**: `IPaymentGateway`, `IObjectStorage`,
  `IEmailSender`/`ISmsSender`/`IWhatsAppSender` all have demo implementations
  registered today (simulate success / log to an in-app "Dev Mailbox" via the
  `NotificationLog` table) and can be swapped for Stripe/Mollie/SEPA, a real S3
  bucket, or SendGrid/Twilio/WhatsApp Business API later via `appsettings`
  config — no business logic changes required.
- **Frontend**: Vite + React + TypeScript, TanStack Query for server state,
  Zustand for client/UI state (auth session, selected branch), React Router,
  shadcn/ui components on Tailwind CSS v4.

## Known environment notes

- This machine's installed .NET SDK reported as a preview channel build
  (`10.0.400-preview...`). The solution targets `net10.0` and builds/runs
  fine on it, but for anything beyond local demo use, install a stable
  channel .NET 10 SDK.
- `react-router-dom` is pinned to v6.30.4 rather than the current v7 line —
  v7 carried a long list of security advisories (mostly SSR/RSC-mode specific,
  which this pure client-side SPA doesn't use). v6 avoids that surface
  entirely. Revisit if upgrading to v7 later.
- **Don't run `dotnet test | tail -N` by hand — use `backend/run-tests.sh`.**
  `dotnet test` at the solution level still runs and prints a normal
  "Passed!" summary for whichever test projects DID build, even if another
  project fails to build entirely (most often: `GymOS.Api.IntegrationTests`
  fails via its `GymOS.API` dependency because a `dotnet GymOS.API.dll`
  server left running from manual verification has the output DLLs locked).
  Piping that output through `tail` makes a partial run look complete — and
  in Bash, it also discards `dotnet test`'s real exit code (`$?` after a pipe
  reflects `tail`, not `dotnet`). `run-tests.sh` stops any process listening
  on port 5000 before building, fails loudly on any build error, and verifies
  every test project under `tests/` actually reported a result before calling
  the run green.
