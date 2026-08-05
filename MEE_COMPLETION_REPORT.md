# Member Experience Engine — Completion Report (Slice 10)

> Per `docs/MEMBER_EXPERIENCE_ENGINE_DESIGN.md` §10's incremental build plan, Slice 10 is
> "hardening + projection rebuild + AI-readiness capture": a rebuild command for every stored
> projection, confirmation that every event needed for future models is actually captured, and a
> pass against the design doc's own §12 acceptance criteria. All ten slices (S1–S10) are now
> shipped. Every verdict below is backed by a check performed for this report — a re-run test
> suite, a live API call, a live UI walkthrough — not a restatement of an earlier slice's claim.

## Overall verdict: **All 8 acceptance criteria pass. MEE is feature-complete.**

Backend: **288/288** tests green (`backend/run-tests.sh`). Frontend: production build clean, zero
TypeScript errors. Ten slices, ten commits, all pushed to `main`.

---

## 1. Acceptance criteria (§12), checked fresh

**1. Logging a workout / checking in visibly increases XP and can advance level, with a matching
`XpTransaction`; repeating the same source event never double-awards.**
Yes. `MemberXpService.AwardAsync` checks `(MemberId, SourceType, SourceId, Reason)` before writing
— proven by `WorkoutXpAwardTests` (idempotent re-publish) and live-verified across every slice this
session touched XP (most recently: the projection rebuild ran against the full 300-member demo
tenant and found only genuine drift, never a duplicate).

**2. Beating a lift creates a Personal Record; mastery %, best weight, est-1RM update for that
exercise, its muscle group, and its machine.**
Yes. `WorkoutProgressionService` (Slice 2) appends `PersonalRecord` rows and recomputes
`ExerciseMastery` per exercise from full history; muscle-group/machine breakdowns are aggregated on
read from `ExerciseMastery` + `Exercise.MuscleGroup`/`Equipment` (never duplicated into their own
tables — confirmed by reading `ExerciseMastery`'s own doc comment while building this slice's
rebuild command). `RebuildExperienceProjectionsTests.Rebuild_recomputes_exercise_mastery_from_workout_history`
proves the projection matches a from-scratch recompute exactly.

**3. Achievements unlock exactly once and appear on the member dashboard with a badge.**
Yes — `MemberAchievement` has a unique `(MemberId, Code)` index; `AchievementService.EvaluateAsync`
checks both the DB and the local change tracker before inserting (the Slice 8 race-condition fix).
Live-verified on the member portal (badge shelf, 5/13 unlocked for the demo member). This slice's
own rebuild command additionally proved the unlock-once guarantee holds under a bulk, cross-member
backfill: running it twice in a row against 300 members backfilled 222 missing achievements on the
first run and exactly 0 on the second (`Rebuild_is_idempotent`, plus the same result live via the
API).

**4. Recovery status reflects recent training load with a plain-language reason; recommendations
always carry an explanation and are overridden by a trainer assignment.**
Yes — `RecoveryPolicy.ClassifyOverall`/`ClassifyMuscleGroup` (Slice 5) always return a reason
string (asserted in `RecoveryPolicyTests`); `RecommendationPolicy` (Slice 6) has no code path that
returns a recommendation without an `Explanation`, and `TrainerPlanActive` suppresses the
self-directed WeeklyFocus/ExerciseSubstitution recommendations whenever an active
`WorkoutAssignment` exists.

**5. Transformation timeline shows measurements, photos, goals, PRs, and achievements in date
order, append-only.**
Yes — `GetMyTimelineQuery` (Slice 7) merges all five sources and sorts `OrderByDescending(OccurredAt)`;
`TransformationTimelineTests` proves ordering and that an unachieved goal is excluded. It's a pure
read composition with no stored table, so "append-only" holds by construction — there's nothing to
mutate.

**6. Trainer dashboard surfaces plateaus/compliance/risks; manager dashboard surfaces
engagement/retention/participation — all branch-isolated.**
Yes — Slice 9's `/api/coaching/{plateaus,compliance,risks}` and `/api/engagement/summary`, all
scoped through `BranchAccessResolver.GetAccessibleBranchIdsAsync`. Branch isolation is
regression-tested explicitly (`Plateaus_excludes_members_outside_the_callers_accessible_branches`,
`Risks_excludes_members_outside_the_callers_accessible_branches`,
`Summary_excludes_members_outside_the_callers_accessible_branches`) — not just asserted, proven by
seeding a second branch's data and checking it never appears.

**7. `/api/me/*` never returns another member's data; all MEE endpoints are permission-gated.**
Yes — every `/api/me/*` handler resolves identity via `MyMemberResolver.ResolveMemberIdAsync`
(Member.UserId ← JWT), never a caller-supplied id; every controller action carries
`[RequirePermission]`. This slice found and closed the one remaining gap in this category:
`JoinChallengeCommand` (Slice 8) checked a challenge existed in the caller's tenant but not that a
branch-scoped challenge belonged to the caller's own branch — fixed and regression-tested
(`Joining_a_challenge_scoped_to_a_different_branch_is_rejected_as_not_found`, commit `1e0d2a3`),
matching the same rule `BookMyClassCommand` already enforced for class sessions.

**8. Every projection is rebuildable from source; no existing table/endpoint changed shape; full
test suite green.**
Yes — see §2 below for the rebuild command itself. No migration was needed for Slices 9 or 10 (both
are pure read-models / an admin command over existing tables); the full suite is 288/288.

---

## 2. The projection rebuild command

`POST /api/experience/rebuild-projections` (`Experience.Manage`), surfaced in **Settings → Data
Maintenance**. Recomputes the two projections that are (a) persisted in their own table and (b)
maintained incrementally by an event handler — the only ones that can drift or need recomputing
after a rule change:

| Projection | Source of truth | Rebuilt by |
|---|---|---|
| `MemberProgression` (TotalXp/Level) | `XpTransaction` ledger (sum) | `MemberProgression.SetTotalXp` — the same rebuild-safe entry point its own doc comment already called for in Slice 1 |
| `ExerciseMastery` | `WorkoutLogEntries` (full history) | `IWorkoutProgressionService.RecomputeMasteryAsync`, exposed publicly for this command and reused as-is from Slice 2 — not reimplemented |

Followed by an achievement backfill pass (`IAchievementService.EvaluateAsync` per member) — a
corrected level or mastery total can newly satisfy a rule that was unmet before.

**Deliberately does not touch** `XpTransaction`, `PersonalRecord`, or `MemberAchievement` rows —
those are themselves append-only ledgers (the source of truth, per §8 of the design doc), not
projections derived from something else. **Recovery status, nutrition adherence, recommendations,
and the transformation timeline need no rebuild step at all**: none of them were ever stored: they
compute fresh from source data on every request (confirmed while researching this slice — every one
of their query handlers' own doc comments already says so explicitly).

**Idempotent by construction**, proven three ways: the application-test suite
(`RebuildExperienceProjectionsTests`, 6 tests), a live double-run against the real 300-member demo
tenant (222 achievements backfilled on the first run, 0 on the second — see below), and the UI
itself surfacing that exact result in a toast.

**A real bug this command caught while building it**: the first implementation ran the achievement
backfill pass in the same unsaved unit of work as the progression rebuild.
`AchievementService.BuildStatsAsync` reads `MemberProgression.Level` back via a plain EF query
(shared with the live event-handler path), which doesn't see a tracked-but-unsaved change — so
newly-corrected levels were invisible to the backfill pass that was supposed to react to them. Two
of the six new tests failed on the first run and pinpointed this exactly. Fixed by saving the
progression/mastery pass before starting the achievement pass — the same two-phase-save pattern
already used by `JoinChallengeCommand` (Slice 8) for an analogous reason. All 6 tests pass after the
fix; full suite re-confirmed at 288/288.

**A second real, larger finding — from running it against real data, not just tests**: live against
the demo tenant (300 members), the rebuild backfilled **222 missing achievements**. Root cause:
`DemoDataSeeder` inserts historical `AttendanceRecord`/`WorkoutLog` rows directly via `db.Add(...)`
for bulk-seeded members (never through `CheckInCommand`/`LogWorkoutCommand`), so those rows never
raised the domain events that drive achievement evaluation — 222 members had real check-in/workout
history that should have unlocked basic achievements (e.g. "Welcome In") but never did. This is
exactly the scenario the design doc's rebuild command exists for, and it fixed it live on first run;
the second run confirmed 0 further backfills.

---

## 3. AI-readiness data capture (§13 — capture only, no models)

The design doc's aspiration is that `XpTransaction`, `PersonalRecord`, mastery snapshots, recovery
classifications, nutrition adherence, and challenge outcomes exist as "clean, timestamped,
per-member series" — training substrate for a future model, not a promise that each is its own
stored table. Auditing what actually exists against that list:

| Series | Stored as | Shape |
|---|---|---|
| XP events | `XpTransaction` (append-only) | `(MemberId, Amount, Reason, SourceType, SourceId, OccurredAt)` — one row per award, never mutated |
| Personal records | `PersonalRecord` (append-only) | `(MemberId, ExerciseId, Type, Value, WorkoutLogId, AchievedAt)` — one row per PR beaten, full history preserved (never overwritten with the new best) |
| Mastery | `ExerciseMastery` (upserted, not append-only) | `(MemberId, ExerciseId, Sessions, TotalSets, TotalReps, TotalVolume, BestWeightKg, BestEstimatedOneRepMax, LastTrainedAt, UpdatedAt)` — a current-state snapshot, not a series; the series itself is recoverable at any point in time by replaying `WorkoutLogEntries` up to that date, which is why this is a rebuildable projection rather than its own ledger |
| Recovery classifications | *not stored* — derived on read from `WorkoutLog` + `RecoveryLog` | Both raw sources are themselves timestamped per-member series (`LoggedAt`/`LoggedOn`); `RecoveryPolicy.ClassifyOverall` is a pure, deterministic function of them, so any historical recovery classification is exactly reproducible from the raw series — a strictly *better* substrate for a future model than a frozen daily snapshot would be, since the classification rule itself can change without needing to backfill anything |
| Nutrition adherence | *not stored* — derived on read from `MealEntry` + `DietPlan` | Same reasoning: `ConsumedAt`-timestamped `MealEntry` rows plus the `DietPlan` date range are sufficient to reconstruct adherence for any past window under any future rule |
| Challenge outcomes | `ChallengeParticipant` (upserted) | `(ChallengeId, MemberId, JoinedAt, IsCompleted, CompletedAt)` — completion is a one-way flip with its own timestamp, effectively append-only in practice (`IsCompleted` only ever goes false→true) |

**Conclusion**: every series the design doc names is captured, either as its own ledger or as a
deterministically-reconstructible function of ledgers that are captured. No additional table is
needed to satisfy §13 — introducing one (e.g. a daily `RecoveryClassificationSnapshot` row) would
duplicate data already fully recoverable from source and would itself need the exact kind of
rebuild-on-rule-change machinery this slice just built for `ExerciseMastery`, for no additional
information. **No AI is implemented** — every rule cited above is a deterministic policy
(`RecoveryPolicy`, `RecommendationPolicy`, `CoachingPolicy`, `MasteryPolicy`, `XpPolicy`,
`AchievementCatalog`), consistent with the design doc's own explicit scope.

---

## 4. What's left

Nothing in the MEE design doc's slice plan (S1–S10) remains. Everything shipped, tested, and
live-verified: 288/288 backend tests, ten commits on `main`
(`d3d4f11`…`88486a6`…and this slice's own commit), zero pending EF model changes, zero TODO/FIXME
markers introduced by this work.
