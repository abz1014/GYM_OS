# Gate 0 — Verdict

**Closed 2026-08-06.** The gate that runs before any Zero-Logging work, testing the premise itself:
*is manual logging friction really what loses members, and what should we measure?*

---

## What was measured

Capture rate — the share of gym visits that produced a recorded workout — computed from data that
already exists for any gym using GymOS (attendance says someone came; workout logs say something was
recorded). No new data, no setup, no wearables.

**Baseline at close: 34%** over 12 weeks — 6,242 visit-days, 2,108 of them recorded, 0 orphan logs,
flagged reliable. 53 members visit regularly and record nothing.

### The caveat that matters

**This is not evidence about human behaviour.** The demo database is generated, and the 34% is a
*design parameter* — the seeder's persona log-rates (committed 80%, regular 12%, casual 5%,
lapsed 20%) produce it. It is useful as:

- proof the metric is computable and wired into the product, and
- a **regression reference** — if a change makes logging harder, this drops and we see it.

It is **not** customer validation. A capture-rate improvement on seeded data proves the flow works
mechanically and nothing more. Closing that gap needs a pilot gym; until then it stays a logged
open assumption.

---

## What the external evidence says

- ~80% of fitness app users abandon within 3 months; day-one retention averages 20–30%.
- **Sub-30-second logging correlates with 2–3× retention** versus manual entry. This replaced the
  original hand-wavy "under 5 seconds" target.
- Automated sync showed +45% 90-day retention versus manual logging in a chronic-care context.
- **Streaks are the single biggest retention driver**; apps with social features *and* streaks see
  ~5× retention versus solo models.
- Tracking reliability is **"essential infrastructure rather than a differentiator"** — one case cited
  a 40% tracking failure rate that broke streak data and made the whole gamification layer
  untrustworthy.
- Only **12.5–29%** of gym members work with a personal trainer at all.

Sources: Sahha health-app churn; productgrowth.in fitness retention; Orangesoft retention strategies;
Lucid retention metrics; Mindster gamification & churn; RunRepeat and ZipDo personal-training
statistics.

---

## Verdict: premise confirmed, but for a different reason than argued

The original case for doing logging first was "it's the unlock for the data everything else feeds
on." The evidence points somewhere sharper.

Streaks and social are the strongest levers, **and GymOS already has both** — streaks, challenges and
a branch leaderboard were built in earlier slices. But before Step 0.5 they were *fiction*: 214 of
215 members had a streak computed from nothing. Shipping story surfaces on top of that would have
reproduced exactly the failure the research describes — a gamification layer nobody can trust.

**So logging stays first, because it makes the levers we already own true.** That also settles the
open ordering question: Story does not jump the queue.

---

## What this gate changed before any feature was built

1. **Killed the original Step 1 design.** "Did you complete today's plan?" served *nobody* — the
   database had 0 workout templates and 0 assignments, and externally only 12.5–29% of members have a
   trainer. Step 1 was rewritten to three tiers with **"repeat last session" as the majority path**,
   trainer-plan confirmation as an enhancement, and a starter picker for first-timers.
2. **Added the zero-prerequisite rule.** GymOS must work for a gym with no trainer plans, no
   wearables, no machine sensors and possibly no check-in data. Anything richer is a layer on top,
   never the spine.
3. **Retargeted to sub-30 seconds**, the threshold with evidence behind it.
4. **Inserted Step 0.5.** With one member holding every workout log, the member experience could not
   be demonstrated at all — a blocker independent of any feature.

---

## The instrument

`GET /api/reports/logging-capture` → Reports ▸ Engagement ▸ *Workout capture rate*.

- Definition lives in `CaptureRatePolicy` (pure, unit-tested) so the report, the member surfaces and
  any later analysis count a "session" the same way — **days on both sides**, matching
  `WeeklyGoalPolicy`, so a double swipe or a split workout can't move the number.
- Reports a **reliability flag**. Capture rate is trivially gameable: make confirmation frictionless
  enough and it climbs while recording workouts that never happened. Sessions logged on days with no
  visit are the tell, and above 20% the report says on its face that the rate has stopped describing
  gym behaviour.
- Surfaces **members who visit but never log** — the population one-tap logging is aimed at, and the
  most actionable line on the report for a gym owner.

---

## Open assumptions carried forward

| Assumption | Closes when |
|---|---|
| Logging friction is a material driver of *this* product's churn | A pilot gym runs for 4+ weeks |
| 34% is a plausible starting capture rate for a real gym | First real tenant's baseline |
| One-tap confirmation raises capture without inflating it | Reliability flag stays green on real data |

**Next: Step 1 — Zero-Logging core**, measured against 34% and against the reliability flag, not
capture rate alone.
