# Member Experience Engine — Design & Execution Plan

Design deliverable for `MEMBER_EXPERIENCE_ENGINE_EXECUTION_BLUEPRINT.md`. It completes the
blueprint's **Phase 0 (study existing architecture)**, reconciles the blueprint's 21 requested
entities against what GymOS already ships, and lays out a concrete, incrementally-shippable build
plan. Nothing here redesigns an existing module — the Member Experience Engine (MEE) is a
**progression/gamification layer that hangs off existing events**, per the blueprint's rule
*"Do not redesign existing modules … integrate cleanly."*

> Status legend used throughout: **REUSE** (exists, use as-is) · **EXTEND** (exists, add fields/logic) ·
> **NEW** (build) · **PROJECTION** (derived read-model, not a source-of-truth table).

---

## 1. Phase 0 — Existing architecture (findings)

**Stack & layering.** ASP.NET Core (net10.0) Clean Architecture: Domain → Application → Infrastructure
→ API. CQRS via MediatR with a 6-behavior pipeline (TenantScope → BranchScope → Logging → Validation
→ Transaction → Audit). EF Core + PostgreSQL. React + TS + Tailwind + shadcn/ui frontend. Multi-tenant
(`ITenantScoped`) and multi-branch (`IBranchScoped`) via EF global query filters keyed off
`ITenantProvider`.

**Two facts that shape this entire design:**

1. **A domain-event backbone already exists but is 100% dormant.** `Common/AggregateRoot.cs`,
   `Common/DomainEvent.cs`, `Common/IHasDomainEvents.cs` are all present with `AddDomainEvent` /
   `DomainEvents` / `ClearDomainEvents`, but **no entity extends `AggregateRoot` and nothing is ever
   dispatched**. `GymOsDbContext.SaveChangesAsync` today only stamps `IAuditable` and auto-fills
   `TenantId` on added `ITenantScoped` entities. → The MEE's "event-driven progression" requirement
   is satisfied by **activating this existing backbone**, not inventing a new one.

2. **A large share of the blueprint's 21 entities already exist**, because Steps 8–12 of the prior
   roadmap already delivered goals, streaks, progressive-overload suggestions, measurements, progress
   photos, and workout/nutrition logging. Treating these as REUSE/EXTEND (not NEW) is what keeps the
   MEE from forking the data model.

**Conventions the MEE must honour (observed in code):**

- **Pure, unit-tested domain policies** for all rules: `StreakCalculator`, `ProgressiveOverloadPolicy`,
  `LeadScorePolicy`, `ChurnRiskPolicy`, `BillingRetryPolicy`, `ClassBookingPolicy`,
  `ClassSessionPlanner`. Every MEE calculation (XP curve, mastery %, recovery score, achievement
  rules) follows this pattern: dates/numbers in → result out, no I/O.
- **Portal security**: the member surface is `/api/me/*`; identity is resolved server-side via
  `MyMemberResolver.ResolveMemberIdAsync` (JWT → UserId → Member) and **no endpoint accepts a
  caller-supplied memberId**; ownership violations return **404, never 403**. All MEE member reads/
  writes reuse this exactly.
- **`IAuditable` interceptor** stamps `CreatedAt`/`CreatedByUserId` on save, but `CurrentUser.UserId`
  is null outside an HTTP context (background jobs, event handlers). Ledger rows written by event
  handlers therefore set their own timestamps explicitly (the same decision `LeadActivity.CreatedAt`
  already made) rather than relying on the interceptor.
- **SQLite is the in-memory test provider** and cannot translate `Max/OrderBy` over `DateTimeOffset`;
  aggregate/order such columns in memory (established precedent in `GetLeadsListQuery`,
  `GetAtRiskMembersReportQuery`, `GetAttendanceHistoryQuery`).
- **`PagedList<T>`** + the shared frontend `Pagination` component for every list surface.

---

## 2. Guiding principles

1. **Additive, never destructive.** No existing table, command, or endpoint changes shape. New tables
   + new event handlers only. Existing commands stay ignorant of the MEE.
2. **Event-driven projections.** Source-of-truth writes stay where they are (WorkoutLog, Attendance,
   MealEntry, …). Domain events fan out to handlers that append to ledgers and refresh projections.
3. **Append-only history; projections are rebuildable.** XP and PRs are append-only ledgers. Level,
   mastery %, recovery status are **projections** that can be recomputed from source events — so a
   rule change never corrupts history (blueprint Phase 14 "never overwrite history / version
   important records").
4. **Rules are pure policies.** XP amounts, level thresholds, mastery %, recovery heuristics,
   achievement predicates all live in pure, unit-tested domain classes.
5. **Safety-first XP.** Per the blueprint: *"Never reward unsafe lifting alone."* Raw weight PRs do
   not by themselves grant the largest XP; XP weights consistency, attendance, recovery, nutrition,
   goal completion, and trainer verification. Un-verified single heavy lifts earn modest XP.
6. **Trainer/human override wins.** Automation produces suggestions and alerts; a trainer decision
   (`TrainerAssignment`, workout plan, verification) always overrides an automated recommendation.
7. **Tenant + branch isolation and the `/api/me` ownership model are inherited unchanged.**
8. **No AI now.** We capture and shape data for future coaching/churn/recommendation models
   (blueprint Phase 15) but implement only deterministic rules.

---

## 3. Entity reconciliation (blueprint's 21 → GymOS)

| # | Blueprint entity | Disposition | Mapping / notes |
|---|---|---|---|
| 1 | MemberLevel | **NEW** (projection) | Current level + total XP, one row per member: `MemberProgression`. |
| 2 | XPTransaction | **NEW** | Append-only ledger row (amount, reason, source ref, occurredAt). |
| 3 | ExperienceLedger | **NEW** (conceptual) | Not a table — the *set* of `XpTransaction`s for a member. |
| 4 | MachineMastery | **NEW** (projection) | Mastery grouped by `Exercise.Equipment` ("machine"). |
| 5 | ExerciseMastery | **NEW** (projection) | Mastery per `Exercise`, derived from `WorkoutLogEntry`. |
| 6 | MuscleGroupMastery | **NEW** (projection) | Mastery grouped by `Exercise.MuscleGroup`. |
| 7 | Goal | **REUSE** `MemberGoal` | Already free-text title + target date + achieved. Do **not** fork. |
| 8 | GoalProgress | **EXTEND** | Optional numeric `TargetValue`/`CurrentValue`/`Unit` on `MemberGoal` **only if** a slice needs quantified goals; otherwise achieved/not is enough. |
| 9 | Achievement | **NEW** | `AchievementDefinition` (catalog) + `MemberAchievement` (unlocked). |
| 10 | Badge | **NEW** | Cosmetic tier/icon attached to an `AchievementDefinition`. |
| 11 | Habit | **REUSE/EXTEND** `StreakCalculator` | Generalize the weekly-streak calc to attendance/workout/nutrition. |
| 12 | Streak | **REUSE** `StreakCalculator` | Already computes weekly streaks from check-ins. |
| 13 | PersonalRecord | **NEW** | Append-only PR ledger per (member, exercise, metric). Seeded/updated from `WorkoutLogEntry`. |
| 14 | ProgressSnapshot | **REUSE** `MemberMeasurement` (+ `ProgressPhoto`) | Immutable dated measurement rows already exist. |
| 15 | Recommendation | **EXTEND** `ProgressiveOverloadPolicy` | Wrap existing overload logic + new plateau/recovery/focus rules into a `Recommendation` read-model. |
| 16 | RecoveryStatus | **NEW** (projection) | Derived from recent workout frequency/volume + rest days. |
| 17 | NutritionCompliance | **PROJECTION** over `MealEntry`/`DietPlan`/`WaterLog` | Step 12 already computes today's macros vs targets; generalize to adherence %. |
| 18 | SkillTree | **NEW** | Catalog of exercise progressions (never locks equipment). |
| 19 | SkillNode | **NEW** | Node in a `SkillTree`, references an `Exercise`, with unlock explanation. |
| 20 | TransformationTimeline | **PROJECTION** | Merge of `MemberMeasurement` + `ProgressPhoto` + `MemberGoal` (achieved) + `PersonalRecord` + `MemberAchievement`, ordered by date. No new source table. |
| 21 | CommunityChallenge | **NEW** | `CommunityChallenge` + `ChallengeParticipant` (opt-in, per branch/tenant). |
| — | FitnessJourney | **PROJECTION** | The member-dashboard aggregate view over all of the above (blueprint mentions it in Phase 1 list; it is the read-model, not a table). |

**Net new source-of-truth tables:** `MemberProgression`, `XpTransaction`, `ExerciseMastery`,
`MuscleGroupMastery`, `MachineMastery`, `AchievementDefinition`, `MemberAchievement`,
`PersonalRecord`, `SkillTree`, `SkillNode`, `CommunityChallenge`, `ChallengeParticipant`. Recovery,
nutrition-compliance, transformation-timeline, and FitnessJourney are **projections/queries**, not
tables. `MemberGoal`/`StreakCalculator`/`MemberMeasurement`/`ProgressPhoto`/`ProgressiveOverloadPolicy`
are reused.

---

## 4. Event-driven architecture

**Activate the dormant backbone.** Selected source aggregates begin extending `AggregateRoot` and
raise events; `GymOsDbContext.SaveChangesAsync` dispatches them after the base save, inside the
ambient transaction that `TransactionBehavior` already opens — so a projection write and its trigger
commit atomically, and a handler failure rolls the whole command back.

```
Command handler (unchanged externally)
  └─ mutates source aggregate (WorkoutLog, AttendanceRecord, MealEntry, MemberGoal, MemberMeasurement)
       └─ aggregate.AddDomainEvent(WorkoutLoggedEvent{ memberId, logId, entries… })
  └─ db.SaveChangesAsync()
       ├─ base.SaveChangesAsync()          // source rows persisted
       ├─ collect IHasDomainEvents.DomainEvents from ChangeTracker
       ├─ publisher.Publish(event)         // MediatR INotification
       │     ├─ AwardXpHandler            → append XpTransaction, refresh MemberProgression
       │     ├─ UpdateMasteryHandler      → upsert Exercise/Muscle/Machine mastery projections
       │     ├─ DetectPersonalRecordHandler → append PersonalRecord if beaten
       │     └─ EvaluateAchievementsHandler → unlock MemberAchievement(s)
       ├─ base.SaveChangesAsync()          // projection rows persisted (same tx)
       └─ ClearDomainEvents()
```

**Domain events (MEE v1):** `WorkoutLoggedEvent`, `MemberCheckedInEvent`, `MealLoggedEvent`,
`GoalAchievedEvent`, `MeasurementRecordedEvent`, `TrainerVerifiedWorkoutEvent`.

**Idempotency.** Each `XpTransaction` carries `(SourceType, SourceId, Reason)`; the award handler is a
no-op if a matching row already exists, so a retry or a re-published event never double-awards.

**Ordering & re-entrancy guard.** Dispatch runs once per outermost `SaveChangesAsync`; handler writes
are collected and saved in a single follow-up `base.SaveChangesAsync` to avoid recursive dispatch.

---

## 5. The engines (all pure policies + thin orchestration)

**XP Engine — `XpPolicy` (pure).**
- **Award table** (per event, tenant-tunable later): attendance check-in, workout completion,
  each consistency milestone (streak weeks), progressive improvement (beat prior session volume),
  logged recovery/rest day, nutrition adherence day, mobility/cardio inclusion, goal completion,
  trainer verification. Values chosen so *consistency + verification* dominate raw load.
- **Anti-abuse:** a single heavy lift with no prior context grants base workout XP only; the
  "progressive improvement" bonus requires a *prior* comparable session (reusing
  `ProgressiveOverloadPolicy`'s last-two-sessions comparison). "Never reward unsafe lifting alone."
- **Level curve:** monotonic increasing XP thresholds (e.g. quadratic `level → cumulativeXp`).
  `LevelForXp(totalXp) → (level, xpIntoLevel, xpForNext)`. Pure and unit-tested.

**Mastery Engine — `MasteryPolicy` (pure).** Per exercise: sessions, sets, reps, total volume
(`Σ sets·reps·weight`), best weight, estimated 1RM (Epley: `w·(1+reps/30)`), PR count, XP, and a
**mastery %** = bounded function of accumulated volume + consistency + recency. Muscle-group and
machine (equipment) mastery aggregate their child exercises and expose balance/weakness signals
(blueprint Phase 5).

**Personal-Record Engine — `PersonalRecordPolicy` (pure).** Given a member's history for an exercise
and a new entry, decide whether it sets a PR on any tracked metric (max weight, est 1RM, max reps at
weight, session volume) and return the delta.

**Achievement Engine — `AchievementCatalog` (pure rules) + evaluator.** Deterministic predicates over
projections: first workout, N-week streak, level milestones, muscle-group balance, goal completion,
challenge finish. Evaluator unlocks only not-yet-earned definitions; unlock is idempotent.

**Recovery Engine — `RecoveryPolicy` (pure).** From recent workout frequency, per-muscle-group volume,
and days since last session, classify `RecoveryStatus` (Fresh / Ready / Fatigued / OvertrainingRisk)
per muscle group and overall, with a plain-language reason. No wearable data required (blueprint
Phase 9 is satisfied with logged data; wearables remain a deferred `IWearableSyncProvider`).

**Recommendation Engine — `RecommendationPolicy` (pure), builds on `ProgressiveOverloadPolicy`.**
Emits typed recommendations: plateau alert, weekly focus (weakest muscle group), volume suggestion,
exercise substitution (from SkillTree), recovery advice. Always carries an **explanation string**
(blueprint Phase 6 "always explain"). Trainer assignments override.

---

## 6. API surface & CQRS (additive)

**Member (`/api/me/*`, gated on `portal.view`, self-resolved):**
`GET /api/me/experience` (level, XP, next-level progress, recent XP ledger) ·
`GET /api/me/mastery` (exercise/muscle/machine breakdown) ·
`GET /api/me/personal-records` · `GET /api/me/achievements` ·
`GET /api/me/recovery` · `GET /api/me/recommendations` ·
`GET /api/me/timeline` (transformation timeline) · `GET /api/me/challenges` +
`POST /api/me/challenges/{id}/join` · `POST /api/me/challenges/{id}/leave`.

**Trainer (gated on `trainers.view`/`workouts.view`):**
`GET /api/coaching/plateaus` · `GET /api/coaching/compliance` · `GET /api/coaching/risks`
(members at overtraining risk / dropping streaks) — read-models feeding the trainer dashboard.

**Manager (gated on `reports.view`/`dashboard.view`):**
`GET /api/engagement/summary` (XP earned, active streaks, challenge participation, level
distribution, retention correlation) — extends the existing analytics surface, does not replace it.

All commands (`JoinChallengeCommand`, admin `CreateChallengeCommand`, `DefineAchievementCommand`, …)
run through the existing MediatR pipeline and permission attributes. A new permission family
`experience.*` (`experience.view`, `experience.manage`) is added to the catalog; **`experience.view`
is granted to the Member role alongside `portal.view`** and to staff, so the member dashboard cards
light up without weakening the `/api/me` ownership model.

---

## 7. Dashboards (wireframe intent)

**Member (extends the existing `/portal` MemberPortalPage — new cards, same page shell):** Level +
XP progress bar; streaks (attendance/workout/nutrition); mastery radar (muscle groups) + top
machines; recovery status banner with advice; personal records list; achievements/badge shelf;
transformation timeline (measurements + photos + PRs + milestones); active challenges. Reuses
`StatCard`, `SimpleBarChart`, the shared `Card`/`Pagination` primitives, and the mobile card-list
pattern.

**Trainer:** plateau list (who's stalled, on which lift), compliance (nutrition/workout adherence),
risk list (overtraining / streak-break-imminent), suggested interventions — each row deep-links to
the member and respects branch isolation.

**Manager:** engagement (XP velocity, active streaks), retention (streak/level vs churn-risk from the
existing `ChurnRiskPolicy`/at-risk report), challenge participation. Rendered in the existing
Reports/Analytics tab shell.

---

## 8. Data architecture & versioned history

- **Append-only ledgers:** `XpTransaction`, `PersonalRecord`. Never updated in place.
- **Immutable snapshots:** `MemberMeasurement`, `ProgressPhoto` (already immutable dated rows).
- **Projections (rebuildable):** `MemberProgression`, `*Mastery`, `RecoveryStatus`,
  nutrition-compliance, timeline. A one-shot rebuild command can recompute every projection from the
  source tables + `XpTransaction`, so rule changes never require destructive migrations.
- **Multi-tenant/branch:** every new table is `ITenantScoped` (member-owned data is tenant-scoped like
  `MemberGoal`); challenge tables are `IBranchScoped` where a challenge belongs to a branch.
- **Auditing:** MEE commands flow through the existing `AuditBehavior`; event-handler ledger writes set
  timestamps explicitly (no HTTP user in scope).

---

## 9. Integration plan (no redesign)

Existing commands are touched **only** to (a) make their aggregate extend `AggregateRoot` and (b) add
one `AddDomainEvent(...)` line — their external contract, validation, and behavior are unchanged.
`SaveChangesAsync` gains a dispatch step guarded so non-MEE saves are unaffected (no events → no-op).
The frontend adds cards/pages; no existing page is restructured. Every new endpoint is additive and
permission-gated. If the whole MEE were reverted, the base product still runs.

---

## 10. Incremental build plan (the slices)

Each slice is one shippable increment in the established rhythm: **domain (pure, unit-tested) →
application (CQRS wired to real DB) → migration → infrastructure/seeding → frontend → full test suite
100% green → drop/migrate/reseed → restart → live-verify (desktop+mobile) → commit → push.**

| Slice | Blueprint phases | Scope |
|---|---|---|
| **S1 — Event backbone + XP/Level core** | 2, 3 | Activate domain-event dispatch in `SaveChangesAsync`; `MemberProgression` + `XpTransaction`; pure `XpPolicy` (curve + award table) with unit tests; `WorkoutLoggedEvent` + `MemberCheckedInEvent` award XP idempotently; `/api/me/experience`; member "Level & XP" card. |
| **S2 — Personal records + exercise/muscle/machine mastery** | 4, 5 | `PersonalRecord` ledger + `PersonalRecordPolicy`; `*Mastery` projections + `MasteryPolicy`; `DetectPersonalRecordHandler` + `UpdateMasteryHandler`; `/api/me/personal-records`, `/api/me/mastery`; mastery + PR cards. |
| **S3 — Achievements + badges** | 3 | `AchievementDefinition`/`MemberAchievement` + `AchievementCatalog`; `EvaluateAchievementsHandler`; `/api/me/achievements`; badge shelf; seed a starter catalog. |
| **S4 — Streaks/habits generalization + nutrition & attendance XP** | 8, 10 | Generalize `StreakCalculator` to workout/nutrition; `MealLoggedEvent` nutrition-adherence XP; nutrition-compliance projection; streak cards. |
| **S5 — Recovery engine** | 9 | `RecoveryPolicy` + `RecoveryStatus` projection; `/api/me/recovery`; recovery banner; XP for logged rest/recovery. |
| **S6 — Recommendation engine + skill trees** | 6, 7 | `RecommendationPolicy` (wraps `ProgressiveOverloadPolicy`) + `SkillTree`/`SkillNode`; `/api/me/recommendations`; recommendations card (with explanations); trainer-override respected. |
| **S7 — Transformation timeline** | 11 | Timeline projection over measurements/photos/goals/PRs/achievements; `/api/me/timeline`; timeline UI. |
| **S8 — Community challenges** | 12 | `CommunityChallenge`/`ChallengeParticipant`; join/leave; challenge XP + achievements; challenge cards; admin create. |
| **S9 — Trainer & manager dashboards** | 13 | Coaching read-models (plateaus/compliance/risks) + engagement/retention read-models; trainer & manager dashboard tabs. |
| **S10 — Hardening + projection rebuild + AI-readiness capture** | 14, 15 | Rebuild command for all projections; ensure every event is captured for future models; docs + acceptance pass. |

Slices ship independently and in order; each leaves the product fully working.

---

## 11. Testing strategy

- **Domain unit tests** for every policy (`XpPolicy`, `MasteryPolicy`, `PersonalRecordPolicy`,
  `RecoveryPolicy`, `RecommendationPolicy`, `AchievementCatalog`) — table-driven, no I/O.
- **Application tests** (SQLite in-memory, real MediatR pipeline) proving: logging a workout awards XP
  and can level up; award is **idempotent** (re-published event ⇒ no double XP); a beaten lift writes
  exactly one `PersonalRecord`; mastery upserts; achievements unlock once; ownership (`/api/me/*` never
  leaks another member) — extending `MemberPortalSecurityTests`.
- **Integration tests** for the new `/api/me/*` and staff endpoints (permission enforcement + self-scoping).
- **Idempotency/rebuild test:** rebuilding projections from source reproduces the same
  `MemberProgression`/mastery as the incremental path.
- The existing suite (currently 177) must stay green at every slice.

## 12. Acceptance criteria

1. Logging a workout / checking in visibly increases a member's XP and can advance their level, with a
   matching `XpTransaction` in the ledger; repeating the same source event never double-awards.
2. Beating a lift creates a Personal Record; mastery %, best weight, est-1RM update for that exercise,
   its muscle group, and its machine.
3. Achievements unlock exactly once and appear on the member dashboard with a badge.
4. Recovery status reflects recent training load with a plain-language reason; recommendations always
   carry an explanation and are overridden by a trainer assignment.
5. Transformation timeline shows measurements, photos, goals, PRs, and achievements in date order,
   append-only.
6. Trainer dashboard surfaces plateaus/compliance/risks; manager dashboard surfaces engagement/
   retention/participation — all branch-isolated.
7. `/api/me/*` never returns another member's data; all MEE endpoints are permission-gated.
8. Every projection is rebuildable from source; no existing table/endpoint changed shape; full test
   suite green.

## 13. AI readiness (capture only, no models)

`XpTransaction`, `PersonalRecord`, mastery snapshots, recovery classifications, nutrition adherence,
and challenge outcomes are all persisted as clean, timestamped, per-member series — the training
substrate for future coaching/churn/recommendation models. **No AI is implemented now** (blueprint
Phase 15); only deterministic policies.

---

## 14. First action

Proceed to **Slice 1 (Event backbone + XP/Level core)** — it activates the dormant domain-event
infrastructure end-to-end and proves the projection pattern with the smallest safe footprint, before
any larger surface is built.
