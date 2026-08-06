# Handoff: GymOS UI/UX Redesign (Member App + Staff Console)

## Overview
A visual and structural redesign of GymOS covering two surfaces:
- **Member app** — mobile-only (390×844 reference), dark-first, built around streaks/ranks/PRs to drive retention.
- **Staff console** — desktop (1440×900 reference), light-first, restructured for a "premium back-office" feel instead of a flat 16-item CRUD sidebar.

Both share one accent (volt `#D6F94A` on ink `#0B0B0C`) and one type system (Archivo + Hanken Grotesk), so the two surfaces read as one product.

## About the Design Files
The bundled `GymOS Redesign.dc.html` (+ `support.js`, required sibling file — open both together, e.g. `open GymOS Redesign.dc.html` from this folder) is a **design reference built in HTML**, not production code. It renders all 9 screens side by side on one canvas for review. **Do not copy its markup into the app.** The task is to recreate these designs inside the existing frontend — React + TypeScript + Tailwind CSS v4 + shadcn/ui (Radix primitives) — reusing its component library, routing, and data hooks, restyled to this system.

## Fidelity
**High-fidelity.** Colors, type, spacing, and copy below are final for this pass. Exact hex values are given rather than approximate; treat them as source of truth over anything you read off a screenshot.

---

## Design Tokens

### Colors
The app already externalizes color as CSS custom properties in `src/index.css` (shadcn convention, consumed via `@theme inline`). Recreate these values as new token values rather than hardcoding hex in components.

| Token | Value | Use |
|---|---|---|
| Ink | `#0B0B0C` | Universal dark surface — member app background, staff sidebar (fixed dark chrome even in light mode), kiosk/front-desk mode, all primary-foreground text on volt |
| Volt (accent) | `#D6F94A` | The one accent color, used everywhere: primary buttons, active nav states, progress rings, streaks, focus rings, badges |
| Volt tint (dark surfaces) | `#2A2A18`, `#26261A`, `#1C1A12`, `#17190F` | Low-opacity accent chips/backgrounds on dark surfaces (notification dots' halo, active day indicator, PR-alert card fill) |
| Success | `#65A30D` (light bg) / `#A3E635` (dark bg) | Up-trends, "active" status, checked-in states |
| Warning | `#D97706` / `#FBBF24` / `#B45309` | At-risk members, expiring memberships, stalled lifts |
| Danger | `#DC2626` / `#F87171` / `#B91C1C` | Overdue invoices, failed/expired states |
| Secondary accent (blue) | `#7DD3FC` | Class/time chips, secondary ring segment — used sparingly, never for primary actions |
| **Staff (light) surfaces** | bg `#F7F7F5`, card `#FFFFFF`, border `#E4E4DE`, muted text `#8A8A80` / `#6E6E66` | |
| **Member (dark) surfaces** | bg `#0B0B0C`, card `#151517`, recessed `#121412`/`#131315`, border `#24242A`/`#26262A`, muted text `#8E8E82`/`#6E6E62`/`#57574F` | |

Mapping to the existing `index.css` tokens: set `--primary: #D6F94A` / `--primary-foreground: #0B0B0C` **globally** (both light and dark blocks) — this replaces the current neutral primary and the scoped `.member-theme` orange override, since both surfaces now share one accent. The existing `.member-theme` class becomes unnecessary and can be removed once this ships. Set `--sidebar`, `--sidebar-foreground`, `--sidebar-primary`, etc. to the fixed ink/dark values **in both the light and dark blocks** — the staff sidebar stays dark chrome regardless of app theme, it does not flip with `.dark`. Keep `--success`/`--warning` tokens (already present) but retune to the hex values above; add a `--danger`/`--destructive` on-dark variant if one doesn't already resolve well.

### Typography
- **Display / numerals / headings**: Archivo, weights 700–900, tight tracking (`letter-spacing: -0.02em` to `-0.04em` at large sizes). Used for all headings, all large numbers (streaks, KPIs, weights, reps), and button labels.
- **Body / UI text**: Hanken Grotesk, weights 400–700. Everything else — labels, descriptions, table cells, nav labels.
- Load both via Google Fonts (`Archivo:wght@400;500;600;700;800;900` and `Hanken+Grotesk:wght@400;500;600;700`) — add the `<link>` tags to `index.html`'s `<head>`, or self-host if the team prefers not to depend on Google Fonts at runtime.
- All metric/numeric values use `font-variant-numeric: tabular-nums` so digits don't jitter in place (dashboards, timers, weights).
- Small uppercase "eyebrow" labels (section headers like "THIS WEEK", "PERSONAL RECORDS") are 10–11px, weight 700, `letter-spacing: 0.1em–0.14em`, muted color.

### Radius & elevation
Current `--radius` base is `0.625rem` (10px) — this redesign runs larger and should bump the base:
- Buttons / pills / inputs: 11–16px
- Standard cards, list rows: 14–18px
- Hero/feature cards (rings card, membership card, KPI panels): 20–24px
- Phone frame corner (design-reference only, not shipped UI): 46px
Shadows stay soft and low-contrast — `0 8px 22px -6px` with a tint of the element's own color for glow (e.g. volt buttons), plain neutral soft shadows elsewhere. Avoid hard drop shadows.

### Icons
The app already uses `lucide-react`. The design reference draws its own inline-SVG icon set purely for portability; map back to these `lucide-react` imports (do not introduce a second icon library):

| Reference icon | lucide-react component |
|---|---|
| ic-home | `Home` |
| ic-plus | `Plus` |
| ic-chart | `TrendingUp` / `BarChart3` |
| ic-grid | `LayoutGrid` |
| ic-flame | `Flame` |
| ic-chev | `ChevronRight` |
| ic-cal | `CalendarDays` |
| ic-trophy | `Trophy` |
| ic-msg | `MessageCircle` |
| ic-check | `Check` |
| ic-search | `Search` |
| ic-bell | `Bell` |
| ic-users | `Users` |
| ic-receipt | `Receipt` |
| ic-gear | `Settings` |
| ic-lock | `Lock` |
| ic-eye | `Eye` |
| ic-arrup | `ArrowUp` |
| ic-dumb | `Dumbbell` |
| ic-apple | `Apple` |
| ic-pin | `MapPin` |
| ic-more | `MoreHorizontal` |
| ic-filter | `Filter` |
| ic-dl | `Download` |
| ic-shield | `ShieldCheck` |
| ic-clock | `Clock` |
| ic-x | `X` |

---

## Screens / Views

### Member app (mobile, dark)

All member screens share: a fixed status bar row (skip — that's iOS chrome, not app UI), 20px horizontal content padding, 14px vertical rhythm between stacked cards, and the redesigned bottom tab bar described under "Navigation" below. Route/component mappings are to `frontend/src/modules/portal/` and `frontend/src/modules/auth/`.

**1. Sign in** — maps to `modules/auth/pages/LoginPage.tsx`.
Full-bleed ink background, radial volt glow top-left. Content bottom-anchored (not centered) so the primary action sits in thumb reach. A 52px rounded-square dumbbell mark in volt, then headline "Welcome back." / muted second line "Week 12 starts now.", then subhead "Sign in to keep your 7-week streak alive." — copy that references streak state should come from context (e.g. last known streak), fall back to plain "Sign in to your account" copy for a first-time or streak-less member. Email field and password field are dark inputs (`background: #151517`, `border: 1px solid #26262A`, 14px radius, 56px tall); the focused field gets a volt border + soft volt glow ring. "Keep me signed in" checkbox (volt check-box) + "Forgot?" link in volt. Primary button "Sign in" full-width, volt fill, ink text, 58px tall, 16px radius. Secondary "Use Face ID" outlined button below it. Footer line "New here? Ask the front desk for an invite." — this app has no self-serve signup (members are provisioned by staff), keep it that way. Reuses shadcn `Input`/`Button`/`Label`, restyled; the demo-role quick-fill buttons currently on this page are a dev convenience and should be dropped or moved behind a dev-only flag in the shipped design.

**2. Home ("Today")** — maps to `modules/portal/pages/TodayPage.tsx`.
Header row: date eyebrow + "{Greeting}, {first name}" (reuse existing `greeting()` helper), a bell icon (with unread dot) and a circular avatar-initial chip on the right.
Hero card (gradient-tinted dark panel, 24px radius): two concentric progress rings side by side conceptually — outer ring = sessions this week (volt), inner ring = volume/secondary metric (`#7DD3FC`) — center numeral is the session count, "of {goal} sessions" caption below. This replaces the existing single-ring `ActivityRing` component with a two-ring variant; either extend `ActivityRing` to accept a second `{value, goal, colorClassName}` layer or compose two instances at different `size`/inset. To the right of the rings: streak number with flame icon, "week streak · best {n}" caption, and the existing goal-edit affordance (opens `WeeklyGoalDialog`). Below the rings, a 7-dot Mon–Sun week strip (volt = trained, ring-outline on volt = today, dark = untrained) — new element, no existing equivalent.
Primary action band: full-width volt pill (16–20px radius) with an ink icon-square, "Start {Workout Name}" + "{n} exercises · {duration} · from your plan" subtext, chevron. This is the promoted primary CTA — replaces the current plain `ConfirmSessionButton`; if there's no assigned plan for the day, fall back to the existing confirm-session / "log a workout" affordance rather than inventing a plan.
Below that: two side-by-side stat chips ("Gym rank #12 ▲4" and "This week 14.2t lifted") — both new, need backing data (rank needs a computed leaderboard position; if not available yet, ship the row without the rank chip rather than fabricate a number). Then the existing "next class today" row (keep as-is, restyle). Then at most one insight card (existing `MyInsight` data from `/api/me/today`) restyled onto the dark surface — keep the existing "at most two insights, none if uncertain" rule from the current implementation's comments.
Keep the existing error/loading states (`CloudOff` retry card, skeletons) — just restyle to dark surfaces.

**3. Active workout / logging** — maps to `modules/portal/pages/LogActivityPage.tsx` + `modules/portal/components/QuickLogWorkout.tsx`. This is new UI; read those files before implementing, since a full set-by-set logger with rest timer does not exist yet — decide with the team whether to build it now or treat it as a fast-follow.
Top bar: close (X) + workout name + "Exercise {n} of {total}" — an elapsed-time chip (ink pill, volt pulsing dot, monospace-style tabular timer) sits at the right. A 6-segment progress bar (one per exercise) below it, volt = done, dim = upcoming.
Exercise header: large Archivo exercise name (2-line wrap), "Last time: {weight} × {reps}, {reps}, {reps}" caption, and an exercise-type icon chip at the right. A PR-callout strip appears when the member is within reach of a record ("Beat {weight} × {reps} to set a PR", amber-tinted, up-arrow icon) — only render when the record math actually supports it.
Set-logging table: header row (Set / Kg / Reps / blank), then one row per set — completed rows show dimmed read-only values + a filled check; the active/current row is visually promoted (brighter surface, volt outline, larger 19px tabular numerals, editable-looking fields) with an empty check circle; a dashed "+ Add set" row at the bottom.
Docked rest-timer sheet at the very bottom (rounded top corners, distinct surface): "Rest" label + large volt countdown, a thin progress bar under it, then two actions side by side — "Skip rest" (outline) and "Log set {n}" (volt fill, primary).

**4. Progress** — maps to `modules/portal/pages/MyProgressPage.tsx`.
Header "Progress" + a 3-way segmented control (Strength / Body / Habits — pill-style, active segment volt-filled). Content below is the "Strength" tab:
Volume chart card: eyebrow "Total volume · 12 weeks", big tabular number + green "▲18%" delta, then a 12-bar bar chart (weekly buckets, bars ramping from dim to volt left-to-right showing the trend), month labels under it.
"Personal records" list: eyebrow + "All lifts" link, then one row per lift — name, relative date ("3 weeks ago" / "Stalled 3 sessions"), current max (tabular, unit as a smaller inline suffix), and a small delta chip (green `+2.5` or neutral `—` for a stalled lift — stalled lifts must read as neutral/cautionary, never as a fabricated positive). Two small stat tiles at the bottom (Weight, Sessions) — swap or extend per what's actually tracked.

**5. Challenge / Leaderboard** — maps to `modules/portal/pages/LeaderboardPage.tsx` + `modules/portal/pages/MyChallengesPage.tsx` (this screen fuses "a specific challenge" + "leaderboard" — confirm with the team whether that merge is in scope, or keep as two routes using the same visual components).
Back-chevron + challenge title. A gradient hero card: "Your position" (huge volt numeral "12" + "of 284" caption) opposite "To top 10" (the exact gap, e.g. "1.8 t") — a thin progress bar and one contextual sentence under it ("6 days left · one more heavy session should do it" — must be computed from real pace/gap data, not generic encouragement). A 3-way scope switcher (My gym / Friends / My age). Ranked list: rank number, initials avatar, name, tabular score — rows 1–3 get a subtle highlight on rank 1; an ellipsis row breaks the list when jumping from top ranks to the member's own position; the member's own row is volt-filled (inverted text) so it's unmissable; a couple of neighbor rows show above/below it. Footer CTA "Invite a friend to this challenge" (outline, full width).

**6. Classes** — maps to `modules/portal/pages/MyClassesPage.tsx`.
Header "Classes" + a horizontal date-picker strip (7 day-chips, selected day volt-filled). Grouped list by time-of-day ("Morning" / "Evening" eyebrows). Each class row: time+duration block (left, divider line), name/instructor/studio, a capacity bar + spots-left caption, and a right-aligned state pill: "Book" (neutral) / "Booked" (volt-filled, check icon) / "Waitlist" (outline, red-tinted spots caption when full). Fullness must drive the visible state — this is the decision surface for booking, not decoration.

**7. More** — maps to `modules/portal/pages/MorePage.tsx` (+ folds in `MyPassportPage`, `MyNutritionPage`, `MyCoachPage`, `AccountPage` as linked rows rather than separate nav destinations, matching the current file's intent).
Header "More" + "{Full name} · {Gym/branch name}" subline. A membership card (gradient dark panel): plan name + "Active" badge, renewal date + branch scope, then a divider and a QR/barcode glyph + member code + a right-aligned "Scan in" shortcut. Below it, two grouped lists exactly like the current implementation's pattern (rounded container, divided rows, icon-chip + label + description + chevron) — "Training" group (My Coach [with an unread-count badge when applicable], Nutrition [with today's kcal], Gym Passport [zones used]) and "Account" group (Payments & invoices, Account & security). Keep the existing `MEMBER_MORE_LINKS` grouping/ordering logic; this is a visual restyle of that same structure, not a new information architecture, aside from promoting membership into its own card at the top instead of a plain link row.

### Navigation (member) — maps to `shared/components/layout/MemberTabBar.tsx` + `shared/nav/memberNav.ts`
Keep the existing `MEMBER_TABS` data, `alsoMatches` active-state logic, and the `aria-current` handling verbatim — only the visual treatment changes. Visually: 4 flat icon+label tabs (Home, Train, Progress, More) become **5 slots**, with a 5th, elevated, circular volt "+" FAB in the center replacing what is currently the 2nd tab ("Log"). Tapping it should open the same log/start-workout flow the current "Log" tab points at (`/log-activity`) — this is a visual promotion of an existing destination, not a new one. Active tab: icon + label both switch to volt and the icon stroke weight increases (mirrors the current `active && 'stroke-[2.5]'` pattern). Bar surface: translucent ink with blur (`backdrop-filter: blur`), 1px top border, safe-area bottom padding preserved from the current implementation.

---

### Staff console (desktop, light)

Shared shell: dark ink sidebar (246px, fixed — does not lighten in light mode) + light content area (`#F7F7F5`). Maps to `shared/components/layout/Sidebar.tsx` + `Topbar.tsx` + `shared/nav/modules.ts`.

**Sidebar restructure**: the current implementation renders `NAV_MODULES` as one flat list of ~16 items (with "Coming soon" badges for Wave 2/3 modules). The redesign groups items under three uppercase section labels — **Operate** (Dashboard, Members, Front desk, Classes, Trainers), **Revenue** (Billing, Memberships, Leads & CRM), **Facility** (Equipment, Maintenance, then a single "More modules +N" row collapsing whatever's left, including "Coming soon" items) — instead of one long undifferentiated list. This is a structural nav change: introduce a `section` field on each `NAV_MODULES` entry (or a small grouping map) and render three labeled clusters. Active item = volt fill, ink text, 11px radius. A couple of items carry a numeric badge (member count, front-desk today-count, overdue-invoice count in a red-tinted chip) — pull these from whatever counts the respective module APIs already expose; don't add new endpoints just for sidebar badges if the number isn't already available somewhere. A branch switcher chip (maps to existing `BranchSwitcher.tsx`) sits above the nav groups; a user/account row (name, role, overflow) is pinned to the bottom of the sidebar, replacing wherever account access currently lives in `Topbar.tsx`.

**8. Dashboard** — maps to `modules/dashboard/pages/DashboardPage.tsx`.
Top bar: search input (with a `⌘K` hint chip — only show it if a command palette actually exists or is planned), a date-range chip, notification bell, "New member" primary button (ink-on-volt... here inverted: on the light top bar the button itself is ink-filled with volt text/icon, matching the sidebar's dark-chrome accent logic).
Page header: "{Weekday}, {date}" + a one-line status summary ("{n} people in the building · {n} invoices need chasing · {n} assets down" — compose from real counts, omit any clause whose count is zero rather than saying "0 assets down") + a small "Live · updated {n}s ago" indicator with a pulsing dot if the dashboard is actually on a live/polling connection (it already has `useDashboardHub.ts` — wire the indicator to that, don't fake liveness if the hook isn't connected).
KPI row: 4 cards (Active members, MRR, Churn risk, Overdue) — each: eyebrow label, big tabular number, small trend delta (colored arrow chip) where a trend exists, and either a tiny sparkline/bar-trend (Active members) or a one-line actionable caption with an arrow ("No visit in 21 days →", "9 invoices · oldest 34 days →"). These captions must link through to the filtered view they describe (e.g. members list pre-filtered to "at risk").
Below the KPIs, a 2-column region: **Occupancy today** (bar chart, hour buckets 6a–10p, today's bars in ink with the peak window highlighted in volt, replace with real check-in-derived data) alongside **"Needs you"** — a queue replacing generic widget cards: a count badge, then 3–5 tinted rows (red-tinted for overdue billing, amber-tinted for expiring memberships/untouched leads, neutral for equipment/maintenance) each with a bold headline and a one-line detail, and a footer "Open action queue" link. Every row must be a real, live query (overdue invoices, expiring-this-week memberships, open work orders, stale leads) — this panel's entire premise is "nothing here is decorative," so don't ship a row backed by static/placeholder text.

**9. Members (list + detail)** — maps to `modules/members/pages/MembersListPage.tsx` + `MemberDetailPage.tsx`. **Structural change**: the redesign fuses these into one master-detail screen (list on the left, a persistent detail rail on the right that updates on row-click) rather than navigating to a separate detail route. Flag this to the team before building — it's a bigger change than a restyle and affects routing (`/members/:id` would need to become a rail state rather than a page, or the rail could simply be a new component that the existing detail page also reuses).
List column: header ("Members" + Export/New-member actions), a search input + a "Filters" button with an active-filter-count badge, then status tabs (All / Expiring / At risk / Frozen / Cancelled, each with a live count). Table: Member (avatar-initials chip + name + code/branch), Plan, Last visit, Status (colored dot+pill: Active green / At risk amber / Frozen neutral / Overdue red), overflow menu. The selected row gets a volt left-edge accent (`box-shadow: inset 3px 0 0` or a real left border) plus a tinted row background. Standard pagination footer.
Detail rail (392px, fixed): dark ink header block (avatar-initials chip, name, "Member since {date} · {code}", close X) with 3 quick actions (Check in — volt primary; Message — outline; overflow). Tab strip (Overview / Billing / Training / Notes). Overview tab: a plan-status card (plan name + Active badge, "{n} days remaining" + progress bar, price/renewal line), a 3-up stat tile row (Visits 30d, Classes, LTV), and a "Recent activity" timeline (icon-chip + one-line event + relative time — check-ins, bookings, payments, staff notes).

**10. Front desk (kiosk mode)** — maps to `modules/attendance/pages/AttendancePage.tsx` + `components/CheckInPanel.tsx`. This screen is a deliberate exception to "staff console is light": it runs full-dark, high-contrast, because it's read at a distance across a counter, not read up close at a desk.
Top strip: brand mark, "Front desk" label, a location/kiosk-id chip, a "Turnstile online" live-status dot, a large clock.
Main (left, wide): a big scan/search input (volt-outlined, blinking-cursor affordance) sits above a result card that only appears once a member is found — avatar-initials chip, name, plan + validity line, then a full-width green "Access granted" confirmation block (icon + headline + "checked in at {time} · {n}th visit this month"), then 3 actions (Open profile / Sell day pass outline, "Next" volt-primary to clear and rescan). Design (not necessarily build now) the equivalent denied/expired-membership state — same layout, red-tinted confirmation block, since front desk staff need that just as much as the happy path.
Right rail (420px): "In the building" counter (big tabular count "/ capacity" + a slim progress bar), a live "Just now" check-in feed (most recent entries, each: avatar chip, name, plan or a flagged note like "Payment overdue · let through", timestamp) — an overdue-but-let-through case is shown amber-tinted so staff see it without the door blocking the member, and a "Next class" card at the bottom (time chip, name, instructor, fill count, a "Roster" shortcut).

---

## Interactions & Behavior
- **Rings/progress** animate on mount via `stroke-dashoffset` transition (the existing `ActivityRing` already does this — keep the `duration-700 ease-out` pattern for any new ring/bar-fill animation).
- **Rest timer** counts down live; "Log set" advances to the next set and restarts/dismisses the timer sheet; "Skip rest" dismisses it immediately. Timer should persist across accidental navigation within the same workout session (don't reset on a re-render).
- **Front-desk scan input** is expected to be a hardware scanner emitting keystrokes into a focused text input, so the field should stay auto-focused and clear itself after each successful/failed lookup, ready for the next scan without staff clicking back into it.
- **Sidebar "Coming soon" items**: keep the current disabled/non-interactive treatment for Wave 2/3 modules, just restyled to fit inside "More modules" rather than the flat list.
- **Leaderboard/challenge copy** ("one more heavy session should do it") must be computed from the member's actual pace/gap — don't ship it as static copy; if the pace math isn't available, drop the sentence rather than generalize it.
- Hover/focus states throughout: volt focus ring (`box-shadow: 0 0 0 3px rgba(214,249,74,.14–.2)`) on focused inputs; row/list hover uses a faint background tint (`hover:bg-accent`-equivalent), consistent with the app's existing hover pattern.
- Standard shadcn/Radix behavior (dialogs, dropdowns, toasts via `sonner`) is unchanged — only their color/radius tokens change, not their mechanics.

## State Management
No new state architecture is implied. Continue using the existing TanStack Query hooks per module (`useMyToday`, `useMyProfile`, dashboard/members/attendance API hooks, etc.) and Zustand `authStore`/branch selection. Net-new pieces of state to add:
- **Active workout/rest-timer session** (screen 3): elapsed time, current exercise index, per-set completion — currently no equivalent exists; scope this as its own feature slice under `modules/portal` rather than bolting it onto `TodayPage`.
- **Members master-detail selection** (screen 9): which member is open in the rail — likely a local `selectedMemberId` state on the list page rather than a route param, if the master-detail merge is approved.
- Everything else (rings, KPIs, feed, badges) is a **restyle of existing data**, not new data.

## Assets
No photography or bitmap imagery is used anywhere in this pass — every visual is type, color, and inline vector icons (see icon mapping table above). If product photography (gym interiors, member photos) is desired for the member card, hero, or empty states in a later pass, that needs to be sourced/shot separately; nothing here assumes it.

## Files
- `GymOS Redesign.dc.html` + `support.js` — the design reference covering all 10 screens (open the `.dc.html` file in a browser; it loads `support.js` from the same folder). Pan/zoom to see each screen; screen labels (`1a`–`1b` groups) and captions under each phone/desktop frame identify what each one is.
- `screenshots/` — a static PNG of each screen, for quick reference without opening the HTML file:
  - `01-login.png` — Sign in
  - `02-home.png` — Home / Today
  - `03-active-workout.png` — Active workout / logging
  - `04-progress.png` — Progress
  - `05-leaderboard.png` — Challenge / Leaderboard
  - `06-classes.png` — Classes
  - `07-more.png` — More
  - `08-dashboard.png` — Staff dashboard
  - `09-members.png` — Staff members (list + detail)
  - `10-front-desk.png` — Staff front desk (kiosk mode)
