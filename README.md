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
`nutritionist@…`, `accountant@…`, `maintenance@…`, `member@…`.

## Frontend setup

```bash
cd frontend
npm install
cp .env.example .env   # set VITE_API_BASE_URL if the API isn't on http://localhost:5000
npm run dev
```

Open `http://localhost:5173` and log in with any demo account above.

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
