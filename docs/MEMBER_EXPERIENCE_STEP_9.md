# Step 9 — Retention review & subtraction

**Closed 2026-08-06.** The roadmap's last member-experience step, and the only one whose brief was
*"build nothing"*: re-measure everything against the Step 0 baseline and remove what didn't earn its
place.

---

## The question the step was supposed to answer, and why it can't be

Step 9 was written to ask "did the number move?" and cut whatever didn't move it. That question is
unanswerable here, and the reason was already recorded at Gate 0: **the demo database is generated,
so capture rate is a design parameter of the seeder, not a fact about people.** No feature built in
Steps 1–8 could move it, and if one appeared to, that would be an artefact.

Deleting features on the strength of a number that cannot respond to them would be worse than not
measuring at all. So the subtraction below runs on criteria that *are* answerable without a pilot:

| Criterion | Answerable now? |
|---|---|
| Did the instrument regress? | **Yes** — it either still reports honestly or it doesn't |
| Is every surface reachable? | **Yes** — routing and navigation are checkable |
| Does one fact get said twice? | **Yes** — and duplication is a defect regardless of retention |
| Does it degrade honestly with thin data? | **Yes** — the zero-prerequisite rule from Gate 0 |
| Is anything wired but unreachable? | **Yes** — dead code is dead whatever the metric says |

"Did return rate improve?" stays open, and stays honest about staying open.

---

## Re-measurement

`GET /api/reports/logging-capture`, same window definition as Step 0:

| | Step 0 baseline | Step 9 | |
|---|---|---|---|
| Capture rate | 34% | **34%** | unchanged |
| Reliability flag | green | **green** | held |
| Orphan log days | 0 | **0** | held |
| Visit-days | 6,242 | 6,242 | — |
| Members visiting without logging | 53 | 53 | — |

**What this proves:** nine steps of member-facing work — one-tap logging, an Undo window, a rewritten
timeline, messaging, a passport, a ranked insight engine — did not break the instrument, did not
inflate the rate, and did not produce a single workout logged on a day with no visit. That last
number is the one that matters: capture rate is trivially gameable by making confirmation
frictionless, and Step 1 made confirmation exactly that. It stayed at zero.

**What this does not prove:** anything about whether members log more. The seeder wrote this history.
A real baseline needs a real gym.

---

## Subtracted

### 1. Two of the five recommendation types

Observed live on the demo member, before the cut — three items on My Training:

```
[PlateauAlert]  Bench Press: ready to add weight
                You've held 60.00kg for two sessions running -- try a small increase next time.
[PlateauAlert]  Tricep Pushdown: ready to add weight
[WeeklyFocus]   Focus on Cardio this week
                Cardio is your weakest trained muscle group at 14% mastery.
```

Every one of the three restated something the member was already reading:

- both overload alerts appeared verbatim in the **Workout Suggestions** card *directly below them* on
  the same page (`Bench Press · Last: 60 kg → try 61.5 kg`, badge "Ready to increase"), and again as
  the home screen's second insight;
- the weakest-group line was the **Mastery** bars beside it, and is the input the Comeback insight is
  built from.

`PlateauAlert` and `WeeklyFocus` are gone — policy methods, enum members, DTO type union, icon map,
and their tests. `RecommendationPolicy` keeps what only it knows: the trainer's plan, the next rung
of a skill tree, a week-over-week volume swing. The card is renamed **"Worth knowing"**, because
that is now what it holds.

On the demo member it renders **nothing at all**, which is the honest outcome: the card was 100%
duplication for them. Verified the surviving path still works by assigning a probe trainer plan —
`Follow your trainer's plan — Your trainer has assigned "Step 9 Probe Plan"` — then deleting both
probe rows exactly.

### 2. Three frontend hooks with no screen

`useMyStreaks`, `useMyAchievements`, `useMyPersonalRecords` — each fetched an endpoint, none was
called by any page. The member is told all three facts, in the place each belongs: records and
achievements inside the session that earned them on the timeline (9 achievement entries and 37
sessions present on live data), the streak on the home screen. A hook nothing calls is a feature that
reads as shipped in the code and is invisible in the app.

The **endpoints stay**. They are self-scoped, tested, and the first thing a second client would ask
for. The unused wiring was the dead weight, not the API.

---

## Repaired — defects the review found

### The timezone fix had a hole in the surface it was written for

`GymDay` was introduced so a member's days are counted on the gym's clock. That sweep touched
`Modules/Portal` and never reached `Modules/Experience`, which owns **recovery and streaks**. So the
home screen was reading its headline insight off a UTC calendar and the weekly ring directly above it
off the gym's — the exact inconsistency the change existed to kill.

Four sites converted, three failing tests written first:

| Site | Symptom |
|---|---|
| `GetMyRecoveryQuery` | An 8:30pm Wednesday session in New York is Thursday in UTC; read Thursday morning it still counted as "trained today" |
| `GetMyStreaksQuery` | A Sunday-evening session deserted the week it finished, emptying a week the member trained and breaking the streak |
| `LogMyRecoveryCommand` | A 9pm rest day was stamped tomorrow — absent from "did you rest today", and the once-per-day guard sidesteppable |
| `GetMyRecommendationsQuery` | Volume-trend window and active-plan comparison off by a day |

The streak test needed a second pass: the obvious version passed by coincidence because both
calendars agreed on the assertion. It now spans two consecutive weeks, where they disagree.

### Two More-screen destinations lit no tab

`MEMBER_TABS`' More entry lists the paths that should keep it active. `/my-coach` (Step 5) and
`/my-passport` (Step 7) were never added as they shipped, so on those two screens **no tab was
highlighted at all** and `aria-current` went with it — the app stopped saying where the member was,
for sighted and screen-reader users alike. Verified fixed in the browser: `aria-current="page"` now
lands on More for both.

### Two comments describing a world that no longer exists

`router.tsx` still said `/portal` renders the membership page "until Phase B replaces it with Today"
(Phase B shipped). `MemberPortalPage` still pointed at a badges grid on My Progress that the portal
split removed.

---

## Kept, deliberately

- **`MyTrainingPage` as a whole.** It overlaps the home screen by design — Home ranks the top two
  things, this is where the member goes to see the rest. Drill-down is not duplication. What was
  duplication was the same sentence twice on one screen, and that is what went.
- **The three orphaned endpoints.** Reachable API, unreachable UI: only the UI was dead.
- **`GetCoachingComplianceQuery` / `GetCoachingRisksQuery` on UTC days.** These aggregate a trainer's
  whole roster; one member's local midnight is the wrong frame. Same call as the staff analytics.

---

## Still open

| Item | Closes when |
|---|---|
| Does any of this move return rate? | A pilot gym runs 4+ weeks. Nothing here answers it |
| In-app messaging is ~60% done | No trainer UI, no notifications, no rate limit or retention policy |
| Staff analytics still bucket by UTC | Deliberate; a separate decision from what a member is told |
| Seed data has 0 workout templates | So `TrainerPlanActive` and the trainer-plan path can't be demoed without a probe |

---

## Verdict

The member experience is internally consistent, honest when the data is thin, and now says each thing
once. 558 tests green; capture 34% with the reliability flag holding and zero orphan logs.

The roadmap is complete through Step 9 — **as an engineering exercise.** Every retention claim behind
it remains an assumption with a pilot-shaped hole in it, and the most useful thing this review can
say is that the instrument to close that hole is built, wired, and still telling the truth.
