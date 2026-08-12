# RECOMMENDATION: The member home screen (`/portal`, `TodayPage.tsx`)

---

## 1. WHAT IS WRONG NOW

### Complaint 2 first, because it is a real bug and it is worse than you think

**Confirmed. The app applies a barbell progression rule to a treadmill, and the treadmill has kilograms on it in your own seed data.**

The chain, verified in code:

1. `backend/src/GymOS.Domain/Workouts/Exercise.cs:5-18` — an exercise carries `Name`, `MuscleGroup`, `Equipment`, `Description`, `VideoUrl`. All nullable free text. **There is nothing typed in the system that distinguishes a treadmill from a bench press.**
2. `WorkoutLogEntry.cs:27-31` — a logged entry has `SetsCompleted`, `RepsCompleted`, `WeightKg`. No duration, no distance, no pace. A run is not representable; the schema forces it into sets × reps × kg.
3. `GetMyWorkoutSuggestionsQuery.cs:58-77` loops **every** exercise the member has logged with no filter on muscle group or equipment.
4. `ProgressiveOverloadPolicy.Evaluate` (`ProgressiveOverloadPolicy.cs:35-58`) compares only `MaxWeightKg` and `TotalReps`.
5. `GetMyTodayQuery.cs:100-101` takes the first `ReadyToIncreaseWeight` verdict, with no type filter, and hands the exercise name plus a suggested kg to `TrainingInsightPolicy.cs:85-92`, which prints `"{ready} is ready to go up"` / `"You've held steady long enough. Try {Trim(next)}kg."`

Your instinct was right but your diagnosis was one step off, and the correction matters. You said "there are no weights and reps in treadmill run." In this database **there are** — `DemoDataSeeder.Wave3.cs:67` defines `("Treadmill Run", "Cardio", "Treadmill")`, and `DemoDataSeeder.MemberActivity.cs:174` assigns weight to anything whose `Equipment` is not the literal string `"Bodyweight"`, falling through to the catch-all at `MemberActivity.cs:265-273` (`15kg + rng.Next(0,5) * 2.5m`). So your database literally contains rows like *Treadmill Run, 4 sets × 9 reps, 17.5 kg*. The app is not inventing a number from nothing — it is faithfully reporting a number the seeder invented, through a policy that had no way to know it was nonsense. That makes this a **schema defect, not a copy defect**, and no amount of string-matching `MuscleGroup == "Cardio"` fixes it (`Cardio` appears in exactly four places in the C# codebase, all of them seed strings — no policy or query reads it, and any real gym typing "Conditioning" defeats it).

It also gets *promoted*: `ProgressiveOverloadPolicy.LeadPriority` (`:77`) puts `ReadyToIncreaseWeight` first, so a stalled cardio row becomes the headline rather than being buried.

**Fix:** add a typed `LoadType` discriminator to `Exercise` (Weighted / Bodyweight / Timed / Distance), filter non-Weighted out of `GetMyWorkoutSuggestionsQuery.cs:58-77`, and stop `MemberActivity.cs:174` keying weight assignment off a magic string. That is an entity change plus an EF migration plus a seeder backfill of the 15 rows at `Wave3.cs:52-69`. Until it lands, **both overload insights come off the home screen** (see §3).

### A second, independent bug in the same card, which is a one-line fix

`GetMyTodayQuery.cs:102-103` populates the plateau signal from `OverloadSuggestion.ConsiderDeload`. I read the policy: `ConsiderDeload` is raised when weight **or reps dropped** (`ProgressiveOverloadPolicy.cs:51-54`). The actual plateau verdict — identical weight and reps two sessions running — is `ReadyToIncreaseWeight` (`:57`), already consumed two lines above. `TrainingInsightPolicy.cs:120-122` then prints *"{X} hasn't moved / Same weight and reps for a while now."*

So the sentence **"Treadmill run hasn't moved" is generated precisely when the numbers did move — downward.** This is true for barbell lifts too; it is not a cardio problem. Worse, the policy's own doc comment (`ProgressiveOverloadPolicy.cs:68-72`) warns that `ConsiderDeload` "is the noisiest of the four" because ordinary day-to-day variation triggers it constantly. Today leads with it anyway. Fix this regardless of what happens to the dashboard, because `/my-training` renders the same verdict.

### Complaint 1: the two log buttons

You are right that they go, but the strongest argument for removing them is not confusion.

Both live in `ConfirmSessionButton.tsx`, mounted twice in `TodayPage.tsx` (`:186` and `:264`, never both). The second one, "Something different" (`ConfirmSessionButton.tsx:103-105`), is a bare `<Link to="/log-activity">` with no data behind it, leading to a destination already reachable three other ways — the tab-bar centre FAB to `/workout` (`memberNav.ts:49`), `/my-training`'s "Start session" (`MyTrainingPage.tsx:494`), and More → "Log something else" (`memberNav.ts:91`). It names an alternative without saying what the alternative is, directly beneath a button whose own subtitle already lists the session it would replace. Cut it, no argument.

The first one is the real issue. **It is the only control in the product that writes numbers the member never entered**, and those numbers immediately become facts feeding personal records, XP, mastery and leaderboard rank. At the RepeatLast and TrainerPlan tiers the loads are at least the member's own history. At the Starter tier they are not: `GetMyNextSessionQuery.cs:102-106` takes `db.Exercises.OrderBy(e => e.Name).Take(3)` at a hardcoded 3×10, so a brand-new member tapping "Start with the basics" silently logs Barbell Squat, Bench Press and Bent-Over Row — three exercises chosen because they sort first alphabetically — for a session that never happened. Under your governing rule that is the single clearest violation on the screen.

Where I will push back: it is not "useless." `ConfirmSessionButton.tsx:26-34` and `VisitPolicy.cs:20-24` both record the product's premise — roughly a third of visits ever get a session attached, and every streak, ring, record and rank is session-derived, not visit-derived. Removing the one-tap commit **will** lower your log rate, and every number on the dashboard I am about to recommend is computed from logs. I am recommending removal anyway, because a home screen that manufactures its own input data is not a dashboard, it is a self-licking ice cream cone. But do not let anyone tell you the cost is zero.

Code-wise it deletes clean: `ConfirmSessionButton` is imported only by `TodayPage.tsx:11`. `WorkoutCelebration` survives — it is independently mounted at `QuickLogWorkout.tsx:376` and `ActiveWorkoutPage.tsx:459`, so the XP/records/challenge readout and the 20-second Undo path are unaffected.

### Complaint 3: is this the important stuff, shown this way?

No. Three specific problems.

**(a) The "Gym rank #3" chip is the one number on the screen a member cannot interpret.** `TodayPage.tsx:279-291` renders a bare `#{rank}` from `useMyLeaderboard('XpEarned','Month')`. What it actually means is *"rank by XP earned so far this calendar month, at your branch, among members who scored above zero"* (`LeaderboardPolicy.cs:40-43`, `:49-57`, `:132-135`). `TotalRanked` and `PercentileFor` are returned in the same payload and thrown away. On a pilot branch that board typically holds 2-4 people, so "#3" invites the member to imagine a pool that does not exist. It also collides with the word "Rank" on the tab bar, which means the *tier ladder* — two different concepts sharing a name on one screen.

**(b) The screen is almost entirely backward-looking and points nowhere.** It reports what already happened and offers one write action. Nothing on it points at My Passport, the rank tier ladder, Mastery, the coach thread, Recovery, or Challenges. Every one of those is fully computed server-side already. `unreadCoachMessages` is *fetched on this exact request* (`GetMyTodayQuery.cs:172-174`, read at `TodayPage.tsx:70`) and never rendered — only a dot on the tab bar reads it.

**(c) The most prominent number is the most discouraging one, and it is the wrong number for most of your members.** The flame prints `WorkoutStreakWeeks`. Your persona distribution (`DemoDataSeeder.MemberActivity.cs:207-225`: 18% Committed, 12% log rate for Regular, 5% for Casual) makes that **0 for the majority of the roster**. Meanwhile attendance is the best-populated fact in the entire system — 90 days of contiguous weekly check-ins for every Active member (`MemberActivity.cs:108-141`) — and `GetMyStreaksQuery.cs:45-48` already computes an attendance streak that no React hook and no page has ever called. The app knows these members have a 9-week attendance streak and instead shows them a black zero.

Two smaller correctness items while we are here: the date eyebrow uses `new Date()` from the **browser** clock (`TodayPage.tsx:20,159`) while every other day on the page is `GymDay.Of(now, zone)` on the **gym** clock (`GetMyTodayQuery.cs:37`, which computes it and discards it); and the weekly goal is settable to 14 (`WeeklyGoalPolicy.cs:23`) while `SessionsThisWeek` counts **distinct days** (`:36-42`) and the ring has exactly 7 arcs — so goals 8-14 are unreachable by construction and `remainingSessions` never reaches zero.

---

## 2. THE RECOMMENDED DASHBOARD

**Philosophy: home is the member's evidence file — what this gym has recorded about them, what has measurably changed, and what of the gym they have not touched yet. Not a to-do list, and never a number they cannot interpret.**

I am taking the **mirror** as the spine, not the next-action design, and here is why the disagreement resolves that way. Once logging leaves home (your complaint 1), home stops being an action surface — the action is one tap on the tab-bar FAB from anywhere in the app. A home screen whose whole job is to say "go train" while having no way to record training is a nag. And your complaint 4 — *force a curiosity to check the rest of the app* — is a pull problem, not a push problem. The mirror pulls. What I am grafting from the next-action design is its **discipline**: one sentence where there is currently a contradictory pair, at most one insight instead of two, and an absolute rule that a block renders nothing rather than a zero.

Blocks in order:

**1. Day line — gym day, name, and today's evidence**
Shows "Tuesday 12 August", the member's first name (`GetMyTodayQuery.cs:39-42`, `.FirstAsync` so never null), and beneath it one sentence from the turnstile: *"You checked in at 6:42pm"* / *"You were at the gym today."* Source is `MyTodayDto.Visit` via `VisitPolicy.Classify` (`VisitPolicy.cs:62-90`) over real check-in/check-out pairs (`GetMyTodayQuery.cs:127-134`). Drop the greeting-by-hour (`TodayPage.tsx:35-40`) — it is client-side copy with no data behind it.
This is the best-behaved thing on the current screen and it stays verbatim: it says "you were at the gym", never "you trained", and it filters on check-**in** date (`VisitPolicy.cs:72-77`) so a forgotten check-out cannot leave someone permanently in the building. The date must switch to the server's gym day.
*Opens next:* when `visit.needsRecording` is true (`VisitPolicy.cs:38`), one quiet line — *"Nothing written down for it yet →"* → `/workout`. It **navigates, it does not write**. This keeps the only UI consumer of `needsRecording` alive after `ConfirmSessionButton` is deleted.

**2. Coach row (slim, conditional)**
*"2 unread from your coach →"* → `/my-coach`. `MyTodayDto.UnreadCoachMessages` — a real count of `CoachMessage` rows authored by the trainer with `ReadAt == null` (`GetMyTodayQuery.cs:172-174`). **This number is already in the payload this page pays for on every load.** Rendering it costs one `if`.
*Opens next:* a named human is waiting for a reply. That is a stronger pull than any metric on the page. Renders only when > 0.

**3. What's next — one card, navigation only**
Ranked, mutually exclusive: a class booked today (`MyTodayDto.NextClassToday`, `GetMyTodayQuery.cs:69-87`, filtered server-side on the gym clock) → `/my-classes`; otherwise the proposed session by name only — *"Today's plan: Bench Press 4×6 · 60 kg, +3 more →"* → `/workout`, which already seeds the live session from the identical `useMyNextSession` hook and the same `SESSION_SOURCE_LABEL` (`ActiveWorkoutPage.tsx:186-200`), so the destination is literally the session named on the card.
Two mandatory corrections. The class card must read `MyClassBookingDto.Status` — it is on the wire (`GetMyTodayQuery.cs:81`) and currently ignored, so a **waitlisted** member is told they have a class at 6pm. And when `Source == None`, show a bare "Start a session" link with **no exercise names and no numbers** — printing the alphabetical Starter trio as if it were a prescription is the same violation in a smaller font.
*Opens next:* the training page, which is where recording belongs.

**4. This week — the ring, and both streaks, labelled**
Ring: 7 arcs from `DaysTrainedThisWeek`, count from `SessionsThisWeek`, both reduced from one pull of workout dates (`GetMyTodayQuery.cs:48-53`) by `WeeklyGoalPolicy` (`:36-42`, `:53-59`), so the count and the shape cannot disagree. Denominator from `MemberTrainingPreferences`, default 3 (`GetMyTodayQuery.cs:57-60`) — **capped at 7**. Beneath it, two numbers side by side: *"9 weeks in the building · 2 weeks trained"*, both from `StreakCalculator.CurrentWeeklyStreak` (`StreakCalculator.cs:18-46`) over attendance rows and workout rows respectively.
The two designs disagreed here — one wanted a single streak that falls back to attendance when the workout streak is zero. **I pick showing both, labelled.** The fallback hides the gap, and the gap *is* your product thesis: a third of visits get a session attached. Two labelled numbers cannot be misread; one unlabelled flame invites the member to read the worse one.
*Opens next:* the discrepancy is self-explanatory and points at the training page. Tapping the ring goes to `/my-progress`.

**5. Since you joined**
One line, three totals: *"Member since 29 July · 34 visits · 6 sessions logged."* Member-since off the `Member` row; visits is exactly the count `GetMyProgressQuery.cs:30` already performs; sessions is `workoutDates.Count`, which `GetMyTodayQuery.cs:48-53` already holds in memory and discards.
This exists because it is the only set of figures on the page that is non-zero from the **second visit** and grows monotonically for everyone, including the ~60% of your roster for whom every workout-derived number is empty. Even Casual members have ~11 visits.
*Opens next:* `/my-progress`.

**6. Your map of this gym**
*"You've trained 6 of the 15 movements here"* — plus one named movement they have never touched. `Tried` / `Available` / `PercentCovered` from `GetMyPassportQuery.cs:56-59`. **This query is already sent on every home load** (`GetMyTodayQuery.cs:96`) and only `GoneQuiet` is consumed; the coverage integers are computed and thrown away on this exact request.
This is the direct answer to complaint 4, and it is the only substantive fact on the page that exists for a **day-one member**, because the catalogue leads the query and mastery is joined onto it.
*Opens next:* `/my-passport`, which is currently unreachable from home — a map with blanks in it rather than another chart of what you already did.

**7. Where you stand — the named tier, not the leaderboard**
*"Committed · 1,240 XP to Strong."* From `GetMyExperienceQuery.cs:68-115` (`RankPolicy.StandingFor`, thresholds `RankPolicy.cs:60-70`); the hook already exists (`useMyExperience`, `portalApi.ts:303`). When the member has been docked for absence, this becomes *"You were Strong. One session gets you back"* (`MyRankDto.Current` + `DaysUntilNextDemotion`, `GetMyExperienceQuery.cs:74-79`) — past the 14-day grace only (`RankPolicy.cs:77`), so it never alarms someone facing no penalty.
A named tier has an absolute meaning that does not depend on who else showed up this month or how many people are in the pool. `#3` does not.
*Opens next:* `/my-rank`, with seven rungs above you.

**8. Coming up**
`MyTodayDto.Coming`, unchanged — `AnticipationPolicy.Next` (`AnticipationPolicy.cs:56-93`): next booked class, else a joined challenge within 3 sessions of finishing, else the next XP level within ~3 sessions. This is the best-built block on the current page and the standard the rest should be held to: every tier is real, nearness is an explicit constant, level distance is expressed in sessions rather than XP, and there is deliberately no fallback copy.
*Opens next:* `/my-classes`, `/my-challenges`, `/my-rank`.

**9. One thing worth knowing — exactly one insight, not two**
Only the kinds that are currently sound: `RecoveryAlert` (most-fatigued muscle group, `GetMyTodayQuery.cs:98-99`) and `GoneQuiet` (a movement genuinely untouched 30+ days, `GymPassportPolicy.cs:68`). The two overload kinds are barred from this slot until `LoadType` ships. Recovery leads because it is the only signal on the whole page that changes what **not** to do today.

**10. Phase 2, after `LoadType` lands: Strength — the last thing that got heavier**
*"Bench Press · 62.5 kg · 4 days ago — up from 60 kg on 24 July."* From the append-only `PersonalRecord` ledger, read today by `GetMyPersonalRecordsQuery.cs:42-53`, which groups by (ExerciseId, Type) and takes `.First()`; to say "up from" it keeps the top two rows of the same group. **This must not ship before the discriminator**, or it prints "Treadmill Run · 17.5 kg" and you have shipped your own bug in a nicer font. When it lands it becomes the single best block on the page — it is the only figure that says the work is doing something, and it is the honest version of the card you are complaining about: it reports what **did** move instead of predicting what should.

**Keep verbatim:** the offline/load-failure card (`TodayPage.tsx:133`, `isStale` via `queryTrust.ts:32-34`). Without it a failed request renders a closed-nothing ring and a zero streak, which reads as "you trained nothing" rather than "we couldn't check." **Every new block must sit behind the same guard** — "no weigh-ins yet" on a dropped request is a lie about the member rather than about the network.

---

## 3. WHAT COMES OFF THE PAGE

| Removed | Where it goes / why |
|---|---|
| `ConfirmSessionButton`, both mounts (`TodayPage.tsx:186`, `:264`) and the component | Recording moves to `/workout` and `/log-activity`. `QuickLogWorkout.tsx:207-220` already offers the identical proposal but loads it into an editor for review before it becomes fact — strictly the better version. Deletes clean; `WorkoutCelebration` and the Undo path survive. |
| "Something different" (`ConfirmSessionButton.tsx:103-105`) | Nowhere. Pure navigation to a destination reachable three other ways. |
| "Gym rank #N" chip (`TodayPage.tsx:279-291`) | `/leaderboard`, where `TotalRanked` and `LeaderboardPolicy.PercentileFor` (`:100-109`) give it the denominator it needs. Replaced on home by the named tier (block 7). Also drops the `useMyLeaderboard` call at `TodayPage.tsx:73` — one fewer request on your most-loaded screen. |
| `ReadyForPr` and `Plateau` insights | Stay computed, stay on `/my-training` inside a list a member is reading deliberately. Return to home only after `LoadType` ships **and** the `ConsiderDeload`→"hasn't moved" miswiring is fixed. |
| The second insight card | The slot drops from up-to-two to exactly one. Full ranked list lives on `/my-training`. |
| "Train this week to keep your streak alive" (`TodayPage.tsx:257-259`) | Deleted. Its condition is `streakWeeks > 0 && !goalMet`, so it fires at a member who has already trained twice this week and whose streak is in no danger. |
| Greeting-by-hour (`TodayPage.tsx:35-40`) | Deleted. No data behind it. |
| Weekly goal values 8-14 | Capped at 7 (`WeeklyGoalPolicy.cs:23`, `WeeklyGoalDialog.tsx:19`). Clamp on **read** at `GetMyTodayQuery.cs:57-60` too, or existing stored values keep rendering an unreachable denominator. |
| Standalone "Today" class chip (`TodayPage.tsx:293-315`) | Folded into blocks 3/8, and it gains the Booked-vs-Waitlisted distinction it currently drops. |

**Where the two designs disagreed and I picked against the mirror:** it wanted a weight sparkline and the member's open goal on home. I am cutting both to `/my-progress`. `DemoDataSeeder.Members.cs:146` seeds a measurement only when `i % 5 == 0` — 6 of 30 members, **one row each** — and only the curated `member@` account has a real series (`Members.cs:320-330`); goals are seeded for that same single member (`Members.cs:348-364`). A block that renders its empty state for 29 of 30 members is not a block, it is a placeholder with good intentions. It comes back when there is a reason to believe members are logging weigh-ins — see §5(c).

---

## 4. THE EMPTY CASE

A member who joined last week, has checked in twice, and has logged one workout:

| Block | What they see |
|---|---|
| 1. Day line | *"Tuesday 12 August. Sara."* No visit line unless they are actually in the building today. Identical for a day-one member — name and gym day always exist. |
| 2. Coach row | **Absent.** |
| 3. What's next | Their one previous session by name, → `/workout`. If they have no history at all: a bare *"Start a session"* link with no exercises and no numbers. |
| 4. This week | Ring at 1 of 3, one arc of seven filled. Streaks: *"2 weeks in the building · 1 week trained."* If both are zero, **the streak line does not render** — no flame, no zero, no row. |
| 5. Since you joined | *"Member since 5 August · 2 visits · 1 session logged."* Day one: *"Member since today"* alone — visits and sessions omitted at zero, not printed as "0 visits". |
| 6. Your map | *"You've trained 3 of the 15 movements here."* Day one: *"15 movements in this gym. You haven't logged any of them yet."* Never "0%". |
| 7. Where you stand | *"Newcomer · 700 XP to Regular."* A real position on a real ladder, not an absence. |
| 8. Coming up | **Absent** unless something is genuinely near. |
| 9. Insight | **Absent.** Nothing is fatigued, nothing has gone quiet. |
| 10. Strength (phase 2) | With one session: *"Your first numbers are in — Bench Press 40 kg, Leg Press 80 kg. These are the marks to beat."* Those are literally the weights they typed, framed as a baseline. **No delta until two records exist on the same exercise+metric**, or a first session where every lift is trivially a record renders as a triumph. |

So a near-empty member sees: a date, a name, a next-session link, a one-arc ring with *"2 weeks in the building"*, three growing totals, a gym map with 12 blanks in it, and a named tier with seven rungs above them. Six populated blocks, none of them a zero, none of them fabricated.

**Be clear-eyed about what this costs.** The current page would have given that member a rank chip and two insight cards to look at. I have argued all three are false or uninterpretable, and I stand by it — but "false and interesting" and "true and sparse" are both ways to lose a member. What saves this design in the empty case is blocks 5, 6 and 7, because the visit totals, the passport coverage and the tier name are **the only three facts in the entire system that are true, non-zero and interpretable from the second visit.** That is the whole load-bearing argument. If you disagree with it, the design does not hold.

---

## 5. COST

### (a) Pure frontend rearrangement — no server change

- Delete `ConfirmSessionButton` and both mounts; delete the `celebration` state and the `WorkoutCelebration` mount at `TodayPage.tsx:361`.
- Delete the rank chip and the `useMyLeaderboard` call (`TodayPage.tsx:73`).
- Delete the greeting-by-hour and the streak-nag line.
- Read `MyClassBookingDto.Status` for Booked vs Waitlisted — the field is already on the wire (`GetMyTodayQuery.cs:81`).
- Render `unreadCoachMessages`, which is already in the payload (`TodayPage.tsx:70`).
- Drop the insight slot from two to one and filter out `ReadyForPr` / `Plateau`.
- Cap the goal picker at 7 (`WeeklyGoalDialog.tsx:19`).
- Call the existing `useMyExperience` hook (`portalApi.ts:303`) and `useMyPassport` (`portalApi.ts:956`).
- Put every new block behind the existing `isStale()` guard.

Roughly a day. Most of this is deletion.

### (b) Needs backend work — small, and mostly returning things already computed

1. **`MyTodayDto += GymDay`** — `GetMyTodayQuery.cs:37` already computes it and throws it away. One field. Zero extra reads.
2. **`MyTodayDto += AttendanceStreakWeeks`** — `GetMyTodayQuery.cs:127-132` already materialises every check-in in memory for `VisitPolicy`. One extra `StreakCalculator.CurrentWeeklyStreak` call. Three lines, zero extra queries. Strictly cheaper than wiring up `GET /api/me/streaks`, which re-reads attendance, workouts **and** meal entries this screen does not need.
3. **`MyTodayDto += TotalVisits`, `MemberSince`, `TotalSessionsLogged`** — the last is `workoutDates.Count`, free. The other two are one count and one field off the `Member` row.
4. **`MyTodayDto += PassportTried` / `PassportAvailable`** — `GetMyPassportQuery` is already sent at `GetMyTodayQuery.cs:96` and only `GoneQuiet` is consumed. Two lines.
5. **Clamp the stored weekly goal on read** at `GetMyTodayQuery.cs:57-60`, plus `WeeklyGoalPolicy.cs:23`. Nobody on the pilot is affected (`MemberTrainingPreferences` is never seeded), but it is a real migration concern in production.
6. **Fix `GetMyTodayQuery.cs:102-103`** — stop feeding `ConsiderDeload` into a card that says "hasn't moved". One line, and it should ship whether or not you take the rest of this.
7. *Phase 2:* previous-record retrieval — `GetMyPersonalRecordsQuery.cs:42-53` already groups in memory and takes `.First()`; take the top two and return `PreviousValue` / `PreviousAchievedAt`. Half a day, no schema change.

Request-count note: home currently issues 2-3 requests; this adds `useMyExperience` and `useMyPassport`, taking it to about 4. If that hurts, fold the tier (3 fields) and passport coverage (2 ints) into `MyTodayDto` — the handler already composes four queries via `ISender` at `GetMyTodayQuery.cs:93-96`.

### (c) Needs data the app does not collect at all — do not underestimate this

**This is the expensive part, and it gates the single best block on the page.**

1. **Exercise modality.** `Exercise.cs:5-18` has no typed discriminator, and `WorkoutLogEntry.cs:27-31` has no duration, distance, pace or incline. This is not a labelling gap — **a treadmill run is not representable in your schema.** Fixing it properly means: a `LoadType` enum on `Exercise` + an EF migration + a backfill of the 15 seeded rows (`Wave3.cs:52-69`) + fixing `MemberActivity.cs:174` and `:265-273` which key weight assignment off the literal string `"Bodyweight"` + a filter in `GetMyWorkoutSuggestionsQuery.cs:58-77` + test coverage (`ProgressiveOverloadPolicyTests.cs` currently has **no** zero-weight or cardio case at all, so nothing pins the behaviour today). Call it 1-2 days for the minimum version. Doing it *properly* — adding duration/distance fields so a run has real metrics rather than just being excluded — is a genuinely larger piece of work touching the log editor, the active-session UI, mastery, and the volume metric. **Block 10 cannot ship before at least the minimum version.** Every "phase 2" in this document is downstream of this one item.
2. **Body measurements.** Nothing in the product prompts a weigh-in. Members *can* self-log (`POST /api/me/measurements`), but nothing asks them to, which is why 29 of 30 pilot members have no series. A weight-trend block on home is not blocked by a query — it is blocked by the fact that you have no capture ritual. Build the ritual first, then the block.
3. **Coach messages.** `CoachMessage` is never seeded — zero rows anywhere in the `Seeding` folder. The unread count is real code on a real endpoint, but **it can never be non-zero on the pilot profile**, so block 2 is invisible in every demo until someone writes a message by hand or the seeder writes a few. That is a seeding fix, not a schema one, but it means you cannot demo block 2 today.
4. **Volume understates bodyweight and cardio training.** Volume is sets × reps × (weight ?? 0), so a member training push-ups, pull-ups and planks accrues zero volume, zero mastery contribution and a flat chart. `WeeklyGoalPolicy`'s own doc comment records that this exact flaw once emptied the weekly ring and was fixed *there* by counting sessions — the same flaw still governs mastery, the volume trend, and the `SessionVolume` personal record. Same root cause as (1).

Two things I checked that are **not** problems, so you do not spend time on them: `AchievementService.BuildStatsAsync` counts visits directly off `db.AttendanceRecords` (`AchievementService.cs:72`), not off the XP ledger, so the visit badges do unlock despite the seeder never raising `MemberCheckedInEvent`. And gym-visit XP works fine in production — `CheckInOutCommands.cs:89` raises the event; it is only the *seeder* that writes attendance rows directly, so seeded XP totals understate the model but live ones do not.

---

## 6. WHAT I WOULD NOT DO

**1. I would not "fix" the gym-rank chip by adding the denominator.** The obvious move is `"#3 of 41 this month"` — the data is right there in the payload. I am rejecting it. Even corrected, it is a number whose meaning changes on the 1st of every month for reasons the member cannot see, it depends entirely on who else happened to turn up, and it shares a word with the tier ladder on the tab bar. On a real pilot branch that board holds 2-4 people. `"#3 of 4"` is honest and *worse* — it tells the member the competition they thought they were winning does not exist. Comparative ranking belongs on `/leaderboard`, where a member goes deliberately. Home gets the absolute ladder.

**2. I would not put a macro ring on home**, even though "how much protein have I got left" is genuinely the most action-relevant question the system can answer mid-day. `GetMyNutritionSummaryQuery.cs:28-66` computes it perfectly. But 22 of 30 pilot members have no diet plan (`activeDietPlanName` null, every consumed figure 0), and two of the eight who do are *deliberately* seeded with zero meals to demonstrate the silent-client state. A ring that is empty for 73% of the roster fails your governing rule in the most literal way — it renders a target with nothing behind it. It belongs on `/my-nutrition` until a diet plan is something most members have.

**3. I would not put the badge shelf or the XP number on home.** Thirteen badges are fully computed, fully stored and rendered by literally no page (`GET /api/me/achievements` has no React hook at all). It is tempting precisely because it is free and it is the classic curiosity hook. I am rejecting it for home because a badge grid is a *destination*, not a status line — it wants a screen with room to breathe and locked-badge previews. Build it as a page on `/my-rank` and let block 7 point at it. Same for the raw XP total: a member with 250 XP staring at a bar that has barely moved learns nothing; the tier name tells them where they stand in one word.

**4. I would not relabel the one-tap confirm and keep it.** There is a version of this where you keep the button but soften it — "Log same as last time?" with a confirmation sheet. Do not. The problem is not the label, it is that home is writing training data. Once the member has to confirm anything, the friction argument is gone and you may as well navigate to `/workout`, which does the same job with per-set review and already renders the identical proposal from the identical hook.

**5. I would not ship blocks 4-10 without the `isStale()` guard on each.** This sounds like a footnote and it is not. The current page has exactly one such guard (`TodayPage.tsx:133`) and it is the reason a dropped request does not read as "your streak is gone." Every block I have added has an empty state that is a *statement about the member* — "you haven't logged any of them yet", "no weigh-ins yet". On a failed fetch, each of those becomes a lie. `ConfirmSessionButton.tsx:66-75` already makes this mistake today: a dropped `/api/me/next-session` silently degrades into a generic link with no indication anything went wrong. `ActiveWorkoutPage.tsx:255-269` handles the same case correctly. Copy that pattern into every new block, not just the page shell.

---

### If you only do three things

1. Fix `GetMyTodayQuery.cs:102-103` — one line, stops the app describing a decline as a plateau. Ship today.
2. Delete both log buttons and the rank chip. Deletion only; nothing else depends on them.
3. Add `LoadType` to `Exercise`. Everything good on this page — the strength block, honest overload advice, meaningful volume, mastery that counts bodyweight work — is downstream of it, and nothing else on your roadmap unblocks as much for as little.