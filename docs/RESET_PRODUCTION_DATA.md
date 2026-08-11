# Resetting the production dataset

Rebuilds the Railway database from empty using the **pilot** seed profile: 10 members and 2 trainers
per branch, every one of them with a login, staff logins unchanged.

This is destructive and irreversible. Everything currently in production goes — members, branches,
attendance, invoices, coach messages, and any manual testing done so far. That is the point of it,
but read the backup step before running anything.

The commands below have to be run by you rather than by Claude: they need the Railway Postgres
credential, which lives in the Railway dashboard and nowhere in this repo.

---

## 1. Back it up first

Railway → your project → the Postgres service → **Variables** → copy `DATABASE_PUBLIC_URL` (the
public one; `DATABASE_URL` only resolves inside Railway's network).

```bash
pg_dump "PASTE_DATABASE_PUBLIC_URL_HERE" > gymos-prod-backup.sql
```

Check the file is not empty and keep it somewhere you will still have tomorrow. Restoring is
`psql "<url>" < gymos-prod-backup.sql` against an emptied database.

## 2. Empty the database

Same URL. This drops every table; the schema is rebuilt by the migration in step 3.

```bash
psql "PASTE_DATABASE_PUBLIC_URL_HERE" -c "DROP SCHEMA public CASCADE; CREATE SCHEMA public;"
```

If you would rather not install `psql`, Railway's Postgres service has a **Data** tab with a query
box that runs the same statement.

## 3. Redeploy

Railway → the API service → **Deploy** (or push any commit). The pre-deploy command runs
`--migrate && --seed --pilot`, which recreates the schema and seeds the pilot gym. Watch the
deploy log for `Seeding with the Pilot profile.` followed by `Demo data seed complete`.

The seed runs inside one transaction, so a failure leaves the database empty rather than
half-populated — you can fix and redeploy without cleaning up first.

## 4. Check it

Sign in at the Vercel app as `owner@titanfitness.demo` (`Demo@12345`) and confirm Members shows
**30**, ten per branch. Then sign in as `member15@titanfitness.demo` and confirm you land on the
member portal for the second branch.

If a browser that was open before the reset shows a 403 or an empty console, it is holding the old
branch id in local storage. Clear it:

```js
localStorage.removeItem('gymos-ui'); location.reload()
```

---

## The logins you get

All on `Demo@12345`, all at `@titanfitness.demo`.

| Login | Who |
|---|---|
| `member@`, `member2@` | MBR-00001/2, first branch. These two carry the curated portal history — streak, weight trend, plateaued lift, a trainer's programme |
| `member3@` … `member10@` | rest of the first branch |
| `member11@` … `member20@` | second branch |
| `member21@` … `member30@` | third branch |
| `trainer@`, `trainer2@` … `trainer6@` | two per branch, in branch order |
| `owner@`, `manager@`, `receptionist@`, `nutritionist@`, `accountant@`, `maintenance@` | unchanged |

Member statuses past the first two are a deliberate mix — some frozen, some expired — because those
are states worth testing rather than noise. The member number tells you the branch, which matters:
a login only ever sees its own branch.

## What this dataset will NOT show well

The pilot gym has 30 members and a few weeks of generated history instead of 300 members and 400
days. Revenue charts, retention cohorts, the at-risk list and the leadership dashboards are all
computed from that history, so they will be thin — not broken, just small. That is the trade for
being able to sign in as anybody. `--seed` without `--pilot` puts the sales dataset back, on an
emptied database.
