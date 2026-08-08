# GymOS — UI/UX uplift spec

Companion to `GymOS UI Uplift.dc.html` (the mockup board). This is an **uplift pass on the
shipped redesign**, not a second redesign: the information architecture, the routes, the copy
rules and the honesty rules all stand. What changes is surface craft — depth, motion, the way
data is drawn — plus one new capability layer for staff.

Reference: `docs/DESIGN_HANDOFF_REDESIGN.md` (the pass this builds on). Where the two disagree,
this file wins for anything visual; that file still owns IA and screen inventory.

---

## 0. Constraints this pass accepted

- **No new tokens.** Every colour, font, and radius below already exists in
  `frontend/src/index.css`. One exception is proposed in §2.
- **No fabricated data.** The current code drops the mockup's MRR, streak-best, volume-ring
  denominator, and trend deltas because nothing backs them. That discipline is kept. Where a
  screen below needs data that doesn't exist, it is listed in §7 as work, never drawn as if it
  were free.
- **Same components.** shadcn/Radix primitives, `lucide-react`, TanStack Query hooks. Nothing
  here needs a new UI library.

---

## 1. Why the current UI reads flat

Diagnosis, so the fixes below have a reason:

1. **No light source.** Every card is `bg-card` + `1px border`. On the ink surface a 1px
   `#24242A` border is the only thing separating a `#151517` card from a `#0B0B0C` page — the
   member app renders as a stack of rectangles with nothing above or behind them.
2. **Nothing moves.** Rings animate `stroke-dashoffset` on mount and that is the entire motion
   vocabulary. Numbers snap, rows snap, sheets snap, routes snap.
3. **Charts are inventories, not arguments.** The week strip, the volume chart and the
   check-ins-by-hour bars all render every value at equal weight. A reader has to do the
   comparison themselves.
4. **Redundant drawing.** Today's ring and its 7-dot week strip are two pictures of the same
   array, costing ~90px of the most valuable screen in the product.
5. **Staff console has no speed layer.** Every task is a full navigate → filter → click → act
   cycle. For the highest-frequency role (reception) that is the whole job.

---

## 2. The uplift kit

Six techniques. Applied consistently, they are what makes the difference; applied to one screen
they read as inconsistency.

### 2.1 Edge light — every raised dark surface
```css
box-shadow: inset 0 1px 0 rgb(255 255 255 / 0.06);
```
On light surfaces use `0 1px 2px rgb(11 11 12 / 0.04)` instead. This single line does more for
perceived quality than anything else in this document. Apply to: member cards, hero panel, rest
sheet, kiosk panels, staff KPI cards, staff panels.

### 2.2 Volt bloom — hero moments only
A blurred radial behind (never on) a hero element, at 10–30% opacity:
```css
background: radial-gradient(circle, #D6F94A 0%, transparent 68%);
opacity: .16;            /* .10 rest sheet · .16 Today hero · .30 PR moment */
filter: blur(30px);
```
Rules: at most one per screen; always `aria-hidden`, `pointer-events:none`; it may bleed off the
container edge (that's what sells it). Login gets a second, cooler bloom in `#7DD3FC` at .10 —
the counter-glow keeps the ink from going muddy.

### 2.3 Grain — hero surfaces only
A 140×140 `feTurbulence` tile as a `background-image` at 4–6% opacity, absolutely positioned
over the surface. Ship it as one shared `<GrainOverlay />` component so the data URI exists once.
Never on list rows or tables — it costs legibility at small sizes for nothing.

### 2.4 Coloured shadow on accent elements
Volt buttons and the FAB already do this (`0 8px 22px -6px var(--primary)`). Extend to: the
Today primary action band, the "Log set" button, the PR-moment share button, kiosk "Next", the
staff sidebar active item, and the peak bars in the occupancy chart.

### 2.5 Severity rails
A 3px full-height rail in the row's own tone, on the leading edge:
```css
box-shadow: inset 3px 0 0 <tone>;   /* rows */
```
Used on: staff KPI cards (volt / amber / red — none where nothing is escalating), "Needs you"
rows, selected member rows, the ⌘K active result. This is what lets someone read severity from
across a room without reading a number.

### 2.6 Ramped chart fills
Sequential bars ramp from a dim mix of the accent to full volt, latest bar with a glow. Applied
to the member volume chart. Non-sequential bars (occupancy by hour) stay ink, with only the peak
window in volt.

> **The one proposed token addition.** The `.dark` block comments note the app has no
> `--recessed` and that `--muted` doubles as it. Three surfaces in this pass need both at once
> (rest sheet over card, kiosk rail over panel, week cells over hero). Adding
> `--recessed: #121412` / `--recessed-foreground` and un-overloading `--muted` is ~20 minutes and
> removes a standing source of ambiguity. Optional; everything ships without it.

---

## 3. Motion spec

One easing, three durations. Anything outside this table is a bug.

| Class | Duration | Easing | Used for |
|---|---|---|---|
| Micro | 120ms | `cubic-bezier(.2,0,0,1)` | Press (`scale(.97)`), hover tint, checkbox |
| Standard | 240ms | `cubic-bezier(.2,0,0,1)` | Row promotion, sheet in/out, panel swap, granted block |
| Expressive | 600–700ms | `cubic-bezier(.16,1,.3,1)` | Ring fill, number roll-up, PR bloom, chart bars |

Specifics:
- **Number roll-up.** Any Archivo numeral ≥26px counts from 0 (or from the previous value where
  one exists — the PR weight counts up *from the old record*) over 600ms with `tabular-nums`
  already on. One shared `<CountUp>` hook. Skip when `prefers-reduced-motion`.
- **Chart bars** grow from baseline, staggered 30ms apart, left to right.
- **Route transitions** (member app only): 160ms cross-fade + 8px upward drift. The staff console
  stays instant — a receptionist navigating 200×/shift does not want animation.
- **`prefers-reduced-motion: reduce`** disables roll-ups, drift, bloom drift, and stagger; keeps
  opacity fades and the rest-timer countdown.
- **Press feedback everywhere.** Every tappable surface in the member app gets
  `active:scale-[.97]` at 120ms. It is the cheapest "this feels native" win available.

---

## 4. Member app — screen by screen

### 4.1 Sign in — `modules/auth/pages/LoginPage.tsx`
- Two blooms (volt top-left, `#7DD3FC` bottom-right at .10), grain overlay, both `aria-hidden`.
- Volt bloom drifts 4%/−3% and scales to 1.06 over 14s. Purely ambient; disabled under
  reduced-motion.
- **Brand mark — the one invented element in this pass.** GymOS has no logo anywhere: the app
  draws lucide's `Dumbbell`, `public/favicon.svg` is still the purple Vite bolt, and
  `public/icons.svg` is a leftover social-icon sprite from the template. The board proposes a drawn
  plate-loaded barbell (5 rects, single fill) in the same 52px volt rounded square, used in all
  three places a mark appears — login, sidebar, kiosk header. It is a placeholder for a real
  identity, not one; if the brand ever gets designed, this is the first thing to replace.
- Field focus already has `ring-[3px] ring-ring/20`; add `border-primary` so the border moves too.
- Everything the current file deliberately dropped (Face ID, keep-me-signed-in, streak copy)
  stays dropped.

### 4.2 Today — `modules/portal/pages/TodayPage.tsx` + `shared/components/ActivityRing.tsx`
**The headline change: the ring becomes the week.**

Replace the single-value ring + separate 7-dot strip with one segmented ring — seven arcs, one
per entry in `daysTrainedThisWeek`, day initials outside each arc.

```
r = 76, stroke-width = 13, stroke-linecap = round
circumference = 477.46 → 7 slots of 68.21
per-arc: stroke-dasharray="50 427.5"  stroke-dashoffset="-(i*68.21 + 9)"
group transform="rotate(-90 100 100)"   viewBox="-14 -14 228 228"
```
- Trained arc: `stroke="url(#voltArc)"` (`#EDFF8A` → `#B8DC2B`) +
  `filter: drop-shadow(0 0 7px rgb(214 249 74 / .55))`
- Untrained: `#24242A` · Today, untrained: `#D6F94A` at 26% opacity
- Labels: 10.5px/700, `#57574F`, trained days `#8E8E82`, today `#D6F94A`
- Centre: session count (56px Archivo 900, rolls up) over "of {goal} sessions"

Keep the existing `<span class="sr-only">n of 7 days trained</span>`. `ActivityRing` gains a
`segments?: boolean[]` prop; when absent it renders exactly as today, so the coach/nutrition
pages that use it are untouched.

Rest of the screen:
- Hero panel: `linear-gradient(160deg,#17190F,#151517 46%,#121214)` + edge light + grain + bloom.
- Streak and goal-edit move into a divided footer *inside* the hero — the hero becomes one
  object rather than a ring beside a column.
- Primary action band (`components/ConfirmSessionButton.tsx`) keeps its volt fill; add the coloured
  shadow and an inner top highlight (`inset 0 1px 0 rgb(255 255 255 / .4)`). **Its copy is fixed:**
  title is `SESSION_SOURCE_LABEL[source]` ("Today's plan" / "Same as last time"), subtitle is the
  real entry summary ("Barbell Back Squat 3×8 · 87.5kg +4 more"), and the quiet "Something
  different" ghost link stays beneath it. The mockup's "Start {Workout Name} · {n} exercises ·
  {duration}" has no source: only a trainer plan is named, and no duration estimate exists anywhere.
- **Rank chip and next-class row pair into one 2-up row.** They are both one-line facts and each
  was taking a full-width bar. This is what buys the insight card its place above the fold.
- Insight card gets a volt-tinted gradient (`#1C1A12` → `#151517`) so it reads as the one
  forward-looking thing on a screen of history.

Unchanged: the visit-prompt logic, `promptLeadsScreen`, the "at most two insights", the
error/`CloudOff` state, every rule about omitting numbers with nothing behind them.

### 4.3 Active workout — `modules/portal/workout/ActiveWorkoutPage.tsx`
The set table is the most function-critical surface in the product; it is read mid-set, at arm's
length, sweating. **Restyle only — do not move or rename a control here.**

- **Active row gets three simultaneous signals**: `1.5px` volt border, a 4px outer halo
  (`0 0 0 4px rgb(214 249 74 / .10)`), and a coloured drop shadow. Fields grow to 21px Archivo 800
  on an ink ground with a `#3A3E1F` border.
- **The 48px log button keeps its filled treatment** — `bg-success` once logged, `bg-primary` on
  the set being worked, `bg-muted` ahead, Check icon on the first two. It is the most-tapped
  control on the screen; it must never become an outline. (An earlier draft of this frame drew it
  as a 30px empty circle.)
- Done rows dim to 60%; upcoming keeps full-contrast text with a border only. That ordering is
  already right in the file — keep the comment explaining why.
- **Exercise segments have three states, not two**: `bg-primary` for a completed exercise,
  `bg-primary/40` for the current one, `bg-border` ahead. The uplift adds a soft glow to the
  current segment only.
- **The docked bar always carries `Finish · {n} sets`.** It is the only way to save the session,
  and a member who stops after four of six exercises must not have to reach the end of a plan they
  aren't finishing. The rest countdown and `Skip rest` are *additive* — rendered only while
  `resting`. There is no "Log set n" button in this bar; logging a set is the button inside the row.
- Rest countdown: 30px Archivo 900 with `text-shadow: 0 0 26px rgb(214 249 74 / .35)`, gradient
  bar over the same `restRemaining / REST_SECONDS` fill. **No printed denominator** — `REST_SECONDS`
  is a flat 90s default, not a per-exercise prescription, and printing "of 2:00" would dress a
  constant as a recommendation.
- Copy is fixed by the data: the subline is `Your best: {currentBest}kg` (a single MaxWeight
  record — there is no per-set history on this screen), and the callout is
  `Beat {currentBest}kg to set a personal record.`, shown only when `plannedWeight > currentBest`.
  **Reps play no part in that comparison** — don't add them to the copy.
- Motion: row promotion 240ms; completed row fades to 60%; the rest block slides in above the
  finish button rather than replacing it.

### 4.4 After a session — `modules/portal/components/WorkoutCelebration.tsx`
**Restyle only. Do not restructure this screen.** Its content and order are dictated by
`MyWorkoutResult`, and the component picks its own headline from that payload: goal just met →
"That's your week", else level-up → "Level {n}", else a record → "New personal record", else the
session's own `character` ("Push day"). Then the sessions ring, then the two figures that always
exist (`xpEarned`, `workoutStreakWeeks`), then `newRecords` / `newAchievements` /
`challengeProgress` — each section rendered only when non-empty.

Surface changes: volt bloom + grain on the ink background, gradient ring stroke with a drop-shadow
glow, edge-lit stat tiles and section cards, sticky footer on a blurred ink ground.
Motion: bloom `scale(.9) → 1` over 600ms, ring fills, XP counts up from 0, the record row lands
last.

**"I didn't do this" stays, and stays quiet.** One tap makes logging easy and a mis-tap equally
easy; an accidental confirmation mints XP and can register a record that never happened. It is the
only thing beside Done, and nothing may compete with the dismiss.

**What `MyWorkoutResult` does not contain**, and therefore what this screen cannot say:
- **No previous best and no record date** — `newRecords` is `{exerciseName, type, value}`. So no
  "+2.5 kg on your best, set 6 weeks ago", and no count-up from the old record.
- **No duration, set count or volume.** There is no session recap to draw.
An earlier draft of this frame invented all five. They are listed in §7 as the DTO change that
would make them real, not drawn as if they were.

**Share card** — worth building, but it does not go here as a second button. Put it inside the
record section, on the record itself: render that section to a 1080×1350 PNG client-side and hand
it to the Web Share API (`navigator.share` with `files`, fall back to download). No server work.
A shareable card wants a "beat your old best by {n}" line, which is §7 item 5's `previousValue`.

### 4.5 Progress · Strength — `modules/portal/pages/MyProgressPage.tsx` + `shared/components/TrendChart.tsx`
**Restyle only.** `StrengthTab` already renders everything below; nothing here is new content.

- Eyebrow `Total volume · {VOLUME_DAYS} days`, then the total as a **single formatted string**
  (`34.8t` above 1000kg, `940kg` below — not a number with a detached unit), with `<DeltaChip>`
  beside it and the comparison caption underneath.
- **The delta is real and already ships.** `StrengthTab` compares the second half of the window
  against the first (`((later − earlier) / earlier) × 100`) and states exactly that: "Last 15 days
  vs the 15 before." It returns `null` — no chip at all — when there is no prior half with volume,
  rather than a division by zero dressed up as +100%. Do not remove it, and do not relabel it as a
  period-over-period figure it isn't.
- **The chart is a daily series**, one point per date from `useMyTrainingVolume(30)`, drawn by
  `TrendChart` with `zeroBaseline` — so rest days sit on the floor and the shape carries the
  direction. It is a line/area chart on purpose: "progress over time is a shape, not a set of bars,
  and reading it as bars hides exactly the thing a member cares about." Never redraw it as bars.
- **Uplift is two touches**: the area gradient's top stop moves from 28% to 34% volt, and the line
  gets `filter: drop-shadow(0 0 5px rgb(214 249 74 / .55))`. Plus one behavioural addition — the
  last non-zero point keeps a persistent glowing dot. `TrendChart` shows markers only on hover for
  series over 14 points (30 daily points, most of them rest-day zeros, would read as noise), but
  the member's latest value earns one. Keep the gridlines, the strided X labels and the hover
  tooltip exactly as they are — including the half-cell offset between a label and its point
  (labels sit in equal flex cells, points at `i/(n−1)`). That is the component's own geometry.
- The two tiles are the shipped pair — **Total lifted** ({n} kg) and **Training days** ({n} / 30) —
  rendered only when the series has points.

**Not on this screen: personal records.** `useMyPersonalRecords` is not called here; records belong
to the active-workout screen. There is no "All lifts" link and nothing in this product computes a
stall. Adding a records list would be a good next step — the query exists — but it needs a decision
about what a row can say: with no previous value and no date on the record, "current max" is the
whole of it.

### 4.6 Tab bar — `shared/components/layout/MemberTabBar.tsx`
Structurally correct already. Add only: `active:scale-[.94]` on the FAB, and an icon
cross-fade + 2px lift on tab change. Do not touch `MEMBER_TABS`, `alsoMatches`, or the
`aria-current` handling — the comment in that file explains why it is a `Link` and not a
`NavLink`, and it is right.

---

## 5. Staff console

### 5.1 Dashboard — `modules/dashboard/pages/DashboardPage.tsx`
**There is no desktop top bar, and this pass does not add one.** `Topbar.tsx` is `md:hidden` and
holds only the mobile drawer trigger — branch and account moved into the dark rail, and content
starts at the top of the window. So: no search field, no notification bell (nothing in the shell
sources one), and no "New member" button (member creation is a dialog owned by `MembersListPage`
with no URL to open it; the button could only drop the user on that list to press a second one).

- **KPI severity rails** (§2.5) on Active members (volt), At risk (amber), Overdue (red). Revenue
  gets none — nothing about it is escalating. Add to `shared/components/console/StatTile.tsx` as a
  `tone` prop alongside the existing `captionTone`. Its deliberate absences hold: no trend-delta
  slot, no icon, no sparkline. The summary is a single snapshot with no previous period.
- **Captions stay plain text.** They become links only once §7 item 1 lands; no page in this app
  reads a filter out of the URL today.
- **Check-ins by hour** (`components/CheckInsByHourPanel.tsx`) — note the name: the endpoint counts
  door swipes per hour, and occupancy would need those netted against check-outs members routinely
  never do. There is no capacity denominator either. The panel already draws **two real series** —
  today's narrower bar in front of a muted 4-week-average band, so a bar standing proud of the grey
  is a busy hour — and that structure is kept intact. The uplift adds only: a gradient and coloured
  shadow on the single peak bar, and a dashed `NOW` marker at the current hour (free — the clock is
  client-side). Keep the fixed plot height, the every-other-hour labels, and the window that widens
  past 6a–10p for any hour with real traffic.
- **"Needs you"** (`components/NeedsYouPanel.tsx`): rows lose their 4px left border for the same
  inset rail the KPIs use, on a tinted ground (`#FDF2F2` critical, `#FDF6EC` warning, `#F4F4F0`
  neutral). Six rows now fit where three did. **Headlines, details and links are unchanged** —
  including the four rows that deliberately carry no detail line, the per-row links to
  /billing, /crm, /equipment, /maintenance and /inventory, and the absence of an "Open action
  queue" footer (no such route exists). Do not reintroduce "N have no renewal booked",
  "untouched for 5+ days", or per-asset age: each needs tracking this product does not do.
- Header, "Updated {n}s ago" chip, permission gating, zero-clause omission: unchanged.

### 5.2 Members — `modules/members/pages/MembersListPage.tsx` + `components/MemberDetailPanel.tsx`
The master-detail rail already ships, and the list already drops the first handoff's Plan / Last
visit columns and its Expiring / At risk / Filters controls — each with a written reason. **None
of them come back here.** The columns stay Member (name + `memberCode`) / Email / Phone (2xl) /
Joined / Status; the tabs stay All + the four real `MemberStatus` values, with Expired in warning
tone and Cancelled in destructive; there is no Export action, because no endpoint exports the
directory.

The single addition:
- **Multi-select + docked action bar.** 52 expired memberships is a one-at-a-time job today. A
  checkbox column (visible on hover, persistent once anything is selected) and an ink bar that
  slides up from the bottom edge on first selection.
- **Its actions are limited to operations that already exist per member**: *Freeze memberships*
  (`FreezeMembershipDialog`, per `memberMembershipId`) and *Add note* (`AddMedicalNoteDialog`).
  Batch endpoints for those two are the whole ask.
- **Message and Export are deliberately absent.** `MemberDetailPanel` already documents why
  Message isn't there — `/api/coaching/clients/{id}/messages` is trainer-to-own-client and 403s
  for a receptionist. The report exports cover revenue, attendance, cohorts and at-risk members,
  never the member list.
- Selected row reads three ways, extending the shipped pattern
  (`bg-primary/10 shadow-[inset_3px_0_0_var(--primary)]`): volt inset rail, tinted row, filled ink
  checkbox. The row open in the rail additionally inverts its avatar chip.
- Detail rail: ink header gains a small volt bloom. Its contents are unchanged and stay
  unchanged — plan card, **two** stat tiles (Visits · 30d, Last visit; not three: there is no
  per-member bookings query and no LTV field), the activity feed, contact block. Header actions
  stay status pill + Check in (or "In the gym since {time}") + Full profile.

### 5.3 ⌘K command palette — new
The console's speed layer, and the strongest live demo in the product.

- Opens on `⌘K` / `Ctrl+K` from anywhere in the staff shell. `ESC` closes. `↑↓` navigate,
  `↵` opens a record, `⌘↵` runs an action in place without leaving the current page.
- Two result classes, always labelled: **records** (members, invoices, classes) and **actions**
  (Check in X, New member, Go to Billing · overdue).
- Filtered by the same permission checks as the pages they shortcut — a receptionist must never
  see a billing action in the list.
- Active result carries the volt rail, same as everywhere else.
- Debounce 150ms; show the last five records touched when the query is empty.

### 5.4 Front desk kiosk — `modules/attendance/pages/AttendancePage.tsx` + `components/CheckInPanel.tsx` + `components/FrontDeskRail.tsx`
**This screen cannot deny anybody, and the design must not pretend otherwise.**
`POST /api/attendance/check-in` performs no entitlement check — it resolves a member and stamps a
row. The only outcome it can refuse is "no such member". There is no turnstile, no door
controller, no `IDoorAccessProvider`. Three things the first handoff asked for stay out, and this
pass does not reintroduce them:

- **"Turnstile online".** A green hardware-health dot is the worst possible thing to fake — staff
  would read it as "the door is working". The top strip shows the branch instead.
- **Per-row plan names and an amber "payment overdue · let through" flag in the feed.**
  `GET /api/attendance` returns (id, memberId, memberName, checkInAt, checkOutAt, method). The
  second line carries what the row knows: "In the building", or "Left at {time}".
- **"Sell day pass".** No day-pass, drop-in or single-visit product exists anywhere in the system.

What the uplift actually changes:
- Type scales hard: member name 44px, verdict 34px, clock 30px, scan placeholder 20px. Read
  standing, two metres back, mid-conversation.
- Granted block is its own green surface (`#16260C` → `#12200C`, border `#2C4A17`) with a 60px
  volt-green check disc, and a green bloom behind the whole left panel.
- Scan field keeps its volt border and halo, stays auto-focused, self-clears after every lookup.
- Feed: newest row volt-tinted; rows that have left drop to 60% opacity.
- Capacity denominator and fill bar render only when `Branch.Capacity` is set — a site that never
  filled it in keeps the bare count rather than a ceiling this app invented.
- Motion: feed rows push down and fade in from the top; granted block `scale(.96) → 1` in 240ms.

**The other states are restyle, not redesign.** All four already exist and all get the same
treatment: **Already inside** (amber, offers Check out), **No member found**, **Check-in didn't
save** (a failed request, named as one — never dressed as a membership verdict), and **multiple
matches**. The honest equivalent of "denied" is the amber status strip the panel already renders
above the verdict when a member's record is Frozen / Expired / Cancelled: the check-in still
goes through, and staff decide who walks in.

---

## 6. Applies everywhere

- Focus visible: `0 0 0 3px rgb(214 249 74 / .18)` + `border-primary`. Volt on ink clears
  contrast comfortably; volt on white does not — on light surfaces pair the ring with an ink
  border, never volt text on white.
- Skeletons: replace flat blocks with a 1.4s shimmer sweep at 6% white on dark / 4% ink on light,
  and match the *shape* of what's loading (a ring skeleton is a ring).
- Empty states: icon chip + one sentence of what would put something here + the action that does
  it. No illustrations.
- Hit targets: 44px minimum in the member app (the current code already argues this for the
  goal-edit button — hold that line).

---

## 7. What this needs that doesn't exist yet

Ordered by ratio of value to effort. Nothing above is drawn as if these already shipped.

| # | Work | Unblocks | Rough size |
|---|---|---|---|
| 1 | Filter-from-URL on members + invoices lists | Dashboard KPI captions becoming real links | ~½ day per list |
| 2 | Global search endpoint (members, invoices, classes) | ⌘K palette, top-bar search | 1–2 days |
| 3 | Command registry + palette component | ⌘K palette | 2 days |
| 4 | Batch freeze-membership and add-note endpoints | Members action bar | 1–2 days |
| 5 | `previousValue` on `newRecords`, + share-card renderer | A real "beat your best by {n}", share button | 1½ days |
| 6 | Sidebar reads the dashboard summary, permission-gated | Front desk + Billing count badges, sidebar search row | ½ day |
| 7 | Stored best-ever streak | The "best {n}" the first redesign wanted and had to drop | ½ day |
| 8 | A date on `PersonalRecord`, + records list on Progress | A records section that can say more than "current max" | 1 day |

The ⌘K palette's only entry point is a new search row in the dark rail, under the branch switcher — there is no desktop top bar to put it in. Only two sidebar badges are drawn — Front desk (still checked in) and Billing (overdue) — because
only those two are both actionable and already computed. Members and Equipment counts were
dropped: a headcount you can't act on is chrome.

Items 5, 7 and 8 are the ones that would let previously-dropped elements return. All three are small.
Note that a volume delta is **not** on this list: `StrengthTab` already computes one from the two
halves of its own window.
Neither is required for anything in this document.

---

## 8. Suggested build order

1. **The kit** (§2) + motion primitives (§3) — `<GrainOverlay>`, `<CountUp>`, the shadow and
   rail utilities. Everything else depends on these.
2. **Today's segmented ring** — the single most visible change, and self-contained.
3. **Celebration restyle** — highest emotional payoff per line of code, and pure surface work.
4. **Dashboard rails + occupancy chart** — makes the console demo land.
5. **⌘K palette** (after §7 items 2–3) — the "this product is fast" moment.
6. **Active workout + kiosk polish** — both pure restyles; change no control.
7. **Members bulk actions** (after §7 item 4).

> **A standing rule for this codebase.** Nearly every page carries a comment explaining what it
> refused to draw and why — the list's missing columns, the kiosk's missing verdict, the
> dashboard's missing MRR. Read the component before designing against it: on this product the
> older spec is not the source of truth, the code is, and most of what a fresh mockup wants to
> add has already been considered and rejected for a reason worth keeping.

Steps 1–4 and 6 need no backend work at all.

---

## 9. Second pass — the rest of the member walkthrough, and the list/detail template

Frames `2a` on the board. Same kit, same rules.

### 9.1 More — `modules/portal/pages/MorePage.tsx`
Restyle. Groups, order, labels and descriptions are `MEMBER_MORE_LINKS` verbatim; rows keep their
64px minimum.
- **Membership is the one row promoted to a card**, and it earns the hero treatment (gradient,
  bloom, grain, edge light, volt QR chip) because it is the thing a member physically holds up at
  the front desk — it was previously sitting in the Account list looking like "Account & security".
- Two different records, kept apart: the status pill reads the **member's** status, the date line
  reads the **membership's**, and it says "Renews" only when `autoRenew` is true — "Ends" otherwise.
  A plan that won't renew must not be described as one that will.
- The plan name wraps rather than truncates. "Quarterly Stand…" tells a member less than two lines.

### 9.2 My Classes — `modules/portal/pages/MyClassesPage.tsx`
Restyle. Fullness is the booking decision, so the capacity bar carries the same judgement the
caption states: `bg-success` with room, `bg-warning` at ≤5 places left, `bg-destructive` when full.
- A booked class reads as settled before anything else — volt-tinted ground, volt border, volt
  time. Cancel stays a quiet ghost beneath the pill, never a second button.
- The caption is "Full", not "Full · 4 on waitlist". The schedule endpoint returns whether *you*
  are waitlisted, never how many others are.
- Uplift adds only the edge light and a coloured shadow on the booked pill.

### 9.3 Leaderboard — `modules/portal/pages/LeaderboardPage.tsx`
Restyle. Your row stays inverted volt (`bg-primary`, inverted text) and gains a coloured shadow —
it is the one row a member should never have to search for. Rank 1 keeps its amber tint.
- Four categories, because a single board only motivates whoever is winning it. **The period toggle
  is hidden on `WeeklyStreak`**: a streak is a standing run, not something accumulated in a window,
  so the control would be a lie.
- **"Top n%" only above 20 ranked members.** On a four-person board first place is mathematically
  "top 25%", which reads as an insult; small boards show the raw score instead.
- Position card gets the bloom/grain/gradient treatment; the rank numeral takes a volt text glow.
- **Do not merge with Challenges.** A challenge is something opted into with an end date; a
  leaderboard is a standing comparison. The merged mockup led with "one more heavy session should
  do it", which needs pace-to-target maths only a challenge has.

### 9.4 Billing — the template for twelve more modules
`modules/billing/pages/InvoicesListPage.tsx` + `InvoiceDetailPage.tsx`.

**Build this pair first, then the other twelve list/detail modules are a column list each** —
Equipment, Maintenance, Inventory, CRM, Trainers, Memberships, Classes, Workouts, Nutrition,
Notifications, Challenges, Migration. The detail frame is likewise the parent of Lead, Work order,
Trainer, Import job and Member detail routes.

List:
- The dashboard's severity rail reappears on overdue rows — same 3px inset, same red, same tinted
  ground. It is the only new element, and it is what makes nine rows findable in a page of fifty.
- Outstanding is amber above zero, muted at zero; the due date goes red on an overdue row.
- Every status the list can hold is a tab. Filtering to a state the rows can show but the tabs omit
  is what makes staff stop trusting the filter.
- **No per-tab counts** — `GET /api/invoices` returns no per-status aggregate, so each would cost
  its own request. The pagination line already reports the count for the open tab.
- **No stat row** (total outstanding, collected this month). Summing the visible page produces a
  number that silently changes meaning when you turn the page. Needs an aggregate endpoint — §7.

Detail:
- The totals stack ends in the only line anyone opened the page for. Outstanding gets a tinted well
  with the amber rail and a 22px display numeral — **only while there is something to chase**. A
  settled invoice shows it muted and inline; an amber zero reads as a problem.
- "Record payment" appears only above zero balance; each completed payment carries its own Refund.
- Header stacks member → number → status → dates, so the page answers "whose, which, what state"
  before any table is read.
- Panel vocabulary for every detail route: 20px radius, hairline border, soft shadow, uppercase
  eyebrow column headers, recessed totals well.

### 9.5 Still not drawn
Nine screens with no precedent on the board: **Settings, Reports, Account, Member detail (the full
route), Lead detail, Work order detail, Trainer detail, Import job detail, Attendance History**.
Plus the member app's **My Coach, My Nutrition, Gym Passport, My Training, Log Activity**, and
**Progress' Body and Habits tabs**. Settings and Reports are the two with genuinely new patterns
(a long form, and a report/export surface); the rest inherit from §9.4 or from the member frames.
