import { useState } from 'react'
import { Link } from 'react-router-dom'
import { BatteryLow, CalendarClock, CalendarDays, ChevronRight, CloudOff, Flame, Lightbulb, Pencil, RotateCcw, RotateCw, TrendingUp } from 'lucide-react'

import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { ActivityRing } from '@/shared/components/ActivityRing'
import { Bloom, CountUp, GrainOverlay } from '@/shared/components/uplift'
import { WeeklyGoalDialog } from '@/modules/portal/components/WeeklyGoalDialog'
import { useMyToday, type MyInsight } from '@/modules/portal/api/portalApi'
import { isStale } from '@/shared/lib/queryTrust'

/*
 * Gym times on the GYM's clock, not the device's.
 *
 * These were fixed formatters with no timeZone, so they rendered against whatever zone the phone
 * happened to be in. A Pilates class at 18:00 UTC appeared as "Today at 6:00 PM" in the Coming-up
 * card — which the server formats — and as "11:00 PM" in the Today chip a few pixels above it, read
 * on a UTC+5 device. One class, two times, one screen, and the member has no way to tell which is
 * the one they should turn up for.
 *
 * Built per render from the zone the server sends rather than hoisted as a module constant, because
 * the zone is data now. `timeZone: undefined` is the documented Intl default of "use the device",
 * so a branch with nothing configured behaves exactly as before rather than throwing.
 */
function gymFormatter(timeZone: string | null | undefined, options: Intl.DateTimeFormatOptions) {
  return new Intl.DateTimeFormat('en-US', { ...options, timeZone: timeZone ?? undefined })
}
const arrivalTimeFormat = new Intl.DateTimeFormat('en-US', { hour: 'numeric', minute: '2-digit' })



/** Monday-first, matching the week WeeklyGoalPolicy counts and the order the server returns. */
const WEEKDAY_INITIALS = ['M', 'T', 'W', 'T', 'F', 'S', 'S']

/** Recovery is the one that says "not today", so it reads differently from the rest. */
function InsightIcon({ kind }: { kind: MyInsight['kind'] }) {
  const className = kind === 'RecoveryAlert' ? 'mt-0.5 size-5 shrink-0 text-warning' : 'mt-0.5 size-5 shrink-0 text-primary'
  if (kind === 'RecoveryAlert') return <BatteryLow className={className} />
  if (kind === 'ReadyForPr') return <TrendingUp className={className} />
  if (kind === 'Comeback') return <RotateCcw className={className} />
  if (kind === 'Momentum') return <TrendingUp className={className} />
  return <Lightbulb className={className} />
}

function greeting(): string {
  const h = new Date().getHours()
  if (h < 12) return 'Good morning'
  if (h < 18) return 'Good afternoon'
  return 'Good evening'
}

/**
 * Which day of the Monday-start week today is — the strip needs it to outline today's cell, and
 * deriving it here rather than sending an index keeps the server's payload to the facts.
 */
function todayIndexInWeek(): number {
  return (new Date().getDay() + 6) % 7
}

/**
 * The member's home. Previously this route showed membership status and a member code — real
 * information, but back-office information, and it forced a member to scroll past their admin
 * details to reach anything they'd open the app for. That content now lives on More > Membership.
 *
 * What's left answers one question on sight — "am I on track this week?" — and offers the single
 * action a member is here to take. Everything else is one tap away.
 *
 * Every number here comes from /api/me/today rather than being stitched together from separate
 * calls: the week, the session count and the streak are one consistent answer computed once,
 * server-side, instead of five independently-cached responses the browser had to reconcile.
 *
 * The redesign asked for a second concentric ring ("Volume 42%") inside the sessions ring, a
 * "best {n}" personal-best streak beside the current one, and a "▲4" movement arrow on the gym-rank
 * chip. None of the three has anything real behind it: there is no volume TARGET anywhere in the
 * product to be a denominator, no stored best-ever streak, and no historical rank snapshot to
 * difference against. Each is omitted rather than invented — the same rule the insight engine and the
 * session proposal already follow, and the reason a member can trust the numbers that ARE here.
 */
export default function TodayPage() {
  const today = useMyToday()
  // The rank chip's only source. Same category the Leaderboard page opens on, so a member tapping
  // through from this chip sees the board that produced the number rather than a different one.
  const [editingGoal, setEditingGoal] = useState(false)

  const data = today.data
  const goal = data?.weeklySessionGoal ?? 0
  const sessions = data?.sessionsThisWeek ?? 0
  const remaining = data?.remainingSessions ?? 0
  const goalMet = data?.goalMet ?? false
  const streakWeeks = data?.workoutStreakWeeks ?? 0
  const daysTrained = data?.daysTrainedThisWeek ?? []

  /**
   * When the gym already knows the member was here, say so instead of asking how their week is
   * going. The turnstile records the visit; only about a third of visits ever get a session
   * attached, and every streak, record and ring is computed from the sessions. Someone standing in
   * the changing room having just trained is the one person for whom "2 more sessions to hit your
   * week" is the wrong sentence — the app is asking for something it already has evidence of.
   *
   * It says the member was here, never what they did. That still takes one tap, and inventing the
   * contents of a session from a door swipe would be exactly the dishonesty the proposal avoids.
   *
   * Which is why this says "you were at the gym" and not "you trained". A door swipe is evidence of
   * neither the training nor the session, and claiming the training put the line at odds with the two
   * things directly beneath it — a ring reading "0 of 3 this week" and a streak warning to train this
   * week. Three statements, one screen, and the member is left deciding which one to believe.
   */
  const visit = data?.visit
  const arrivedAt = visit?.checkedInAt ? arrivalTimeFormat.format(new Date(visit.checkedInAt)) : null
  const visitLine = !visit?.needsRecording
    ? null
    : visit.state === 'InGym'
      // The arrival time rather than an elapsed counter. "47 min so far" would only recompute when
      // the query refetches, so it would sit on screen being wrong; a clock time stays true.
      ? arrivedAt
        ? `You're at the gym since ${arrivedAt}. One tap when you're done.`
        : "You're at the gym. One tap when you're done."
      : 'You were at the gym today — want to record it?'

  /*
   * Step 3 of the roadmap in one boolean: the door already told the app they were here, so the
   * record-it prompt leads the screen instead of sitting under the ring where it has to be scrolled
   * for. The prompt itself is unchanged — this only decides where it goes.
   *
   * It moves for both InGym and Visited (needsRecording covers both), because the roadmap asks for
   * the prompt "on the way out", and a member who has already left and not recorded is exactly the
   * person it was meant to catch. Once something is recorded, needsRecording goes false and the
   * screen returns to its normal order rather than nagging.
   */
  const promptLeadsScreen = visit?.needsRecording === true

  /**
   * When the request fails outright there is nothing honest to draw. Falling through to the normal
   * layout would render a closed-nothing ring and a zero streak — which reads as "you have trained
   * nothing this week and your streak is gone" rather than "we couldn't check". For a screen whose
   * whole motivational weight rests on a streak, inventing a zero is the worst thing it could say,
   * so the page says it doesn't know and offers to retry instead.
   */
  if (isStale(today) && !data) {
    return (
      <div className="mx-auto max-w-2xl space-y-5">
        <h1 className="font-display text-3xl font-black tracking-tight">{greeting()}</h1>
        <Card className="rounded-3xl">
          <CardContent className="flex flex-col items-center gap-3 py-10 text-center">
            <CloudOff className="size-10 text-muted-foreground" />
            <p className="font-medium">We couldn't load your week</p>
            <p className="max-w-xs text-sm text-muted-foreground">
              Your training is safe — we just can't reach the gym right now.
            </p>
            <Button variant="outline" className="mt-2 rounded-xl" onClick={() => today.refetch()} disabled={today.isFetching}>
              <RotateCw className={today.isFetching ? 'size-4 animate-spin' : 'size-4'} />
              {today.isFetching ? 'Trying…' : 'Try again'}
            </Button>
          </CardContent>
        </Card>
      </div>
    )
  }

  return (
    <div className="mx-auto max-w-2xl space-y-3.5">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">
            {gymFormatter(data?.gymTimeZone, { weekday: 'long', day: 'numeric', month: 'short' }).format(new Date())}
          </p>
          <h1 className="font-display text-3xl leading-tight font-black tracking-tight">
            {greeting()}
            {data ? `, ${data.firstName}` : ''}
          </h1>
        </div>
        {data && (
          <span className="flex size-11 shrink-0 items-center justify-center rounded-2xl bg-primary font-display text-lg font-black text-primary-foreground">
            {data.firstName.charAt(0).toUpperCase()}
          </span>
        )}
      </div>

      {today.isLoading ? (
        <Skeleton className="h-5 w-56" />
      ) : (
        <p className="text-sm text-muted-foreground">
          {visitLine ??
            (goalMet
              ? "You've hit your goal for the week. Anything else is a bonus."
              : sessions === 0
                ? "Let's get the week started."
                : `${remaining} more session${remaining === 1 ? '' : 's'} to hit your week.`)}
        </p>
      )}

      {/*
        Navigates. It does not write.
        This used to be a one-tap commit that logged a whole session the member never typed — at the
        Starter tier, three exercises picked because they sort first alphabetically, at 3x10, for a
        session that may not have happened. Recording belongs on the training page, where every set
        is reviewed before it becomes fact. The prompt survives because visit.needsRecording is a
        real signal (checked in today, nothing logged); only the button behind it changed.
      */}
      {promptLeadsScreen && (
        <Link
          to="/workout"
          className="press flex items-center justify-between gap-3 rounded-2xl border border-border bg-card p-4 edge-light transition-colors hover:bg-accent"
        >
          <span className="text-sm font-medium">Nothing written down for it yet</span>
          <ChevronRight className="size-5 shrink-0 text-muted-foreground" />
        </Link>
      )}

      {/*
        Hero: the whole "am I on track" answer, without reading a single number twice.

        The ring IS the week now. It used to be a single-value ring beside a streak column, with a
        separate 7-dot strip underneath drawing the same daysTrainedThisWeek array a second time —
        two pictures of one fact, costing about 90px of the most valuable screen in the product. One
        segmented ring carries both the count and the shape of the week, and the streak and goal
        controls move into a divided footer inside the panel so the hero reads as one object.
      */}
      <div
        className="relative overflow-hidden rounded-3xl border border-border edge-light"
        style={{ background: 'linear-gradient(160deg,#17190F,#151517 46%,#121214)' }}
      >
        <Bloom className="-top-16 left-1/2 size-64 -translate-x-1/2" opacity={0.16} />
        <GrainOverlay />

        <div className="relative flex justify-center px-5 py-6">
          {today.isLoading ? (
            // A ring skeleton is a ring — matching the shape of what is loading, per the kit.
            <div className="size-[196px] shrink-0 rounded-full border-[13px] border-border shimmer" />
          ) : (
            <ActivityRing
              value={sessions}
              goal={goal}
              size={196}
              segments={daysTrained.length === 7 ? daysTrained : undefined}
              segmentLabels={WEEKDAY_INITIALS}
              todayIndex={todayIndexInWeek()}
            >
              <CountUp
                to={sessions}
                className="font-display text-[56px] leading-none font-black tracking-tight tabular-nums"
              />
              <span className="mt-1 text-[11px] text-muted-foreground">of {goal} sessions</span>
            </ActivityRing>
          )}
        </div>

        {/* The divided footer. Streak on the left, goal on the right, one hairline between them —
            what turns a ring plus a column of controls into a single panel. */}
        <div className="relative flex items-stretch border-t border-border/70">
          <div className="flex flex-1 items-center gap-2.5 px-5 py-4">
            <Flame className={`size-6 shrink-0 ${streakWeeks > 0 ? 'text-primary' : 'text-muted-foreground'}`} />
            <span className="min-w-0">
              <span className="block font-display text-2xl leading-none font-black tracking-tight tabular-nums">
                {streakWeeks}
              </span>
              <span className="block text-xs text-muted-foreground">week streak</span>
            </span>
          </div>

          {data && (
            <>
              <span className="w-px bg-border/70" aria-hidden />
              <Button
                variant="ghost"
                // Visually secondary, but still a real touch target: everything else a member taps
                // in this shell clears 44px, and this sits right beside the ring on a phone.
                className="press h-auto shrink-0 rounded-none px-5 text-xs text-muted-foreground"
                onClick={() => setEditingGoal(true)}
              >
                <Pencil className="size-3.5" />
                Goal {goal}/week
              </Button>
            </>
          )}
        </div>
      </div>

      {/*
        Rank and today's class pair into one 2-up row. Both are single facts and each was taking a
        full-width bar; pairing them is what buys the insight card its place above the fold.

        Rank comes from the leaderboard the member can actually open, and is rendered only once that
        query has produced a position for them. A member outside the ranked set (nothing logged in
        the window) has no rank, and the chip is dropped rather than showing a dash or a guess —
        which is also why this is one chip rather than the design's pair, since the second was a
        lifted-tonnage figure with no total behind it.
      */}
      {/*
        The gym-rank chip that shared this row is gone, and the class chip is now full width.

        "#3" was the one number on the screen a member could not interpret: it meant rank by XP
        earned so far THIS CALENDAR MONTH, at their branch, among members who scored above zero —
        none of which was on screen. It reset for everyone on the 1st, it vanished silently for
        anyone with no XP, and it shared the word "Rank" with the tab bar, which means the tier
        ladder. Adding the denominator does not save it: on a real branch that board holds a handful
        of people, and "#3 of 4" is honest and worse. Comparative standing lives on /leaderboard,
        which a member opens deliberately.
      */}
      {data?.nextClassToday && (
        <Link
          to="/my-classes"
          className="press flex items-center gap-3 rounded-2xl border border-border bg-card p-4 edge-light transition-colors hover:bg-accent"
        >
          <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-primary/10">
            <CalendarDays className="size-5 text-primary" />
          </span>
          <span className="min-w-0 flex-1">
            <span className="block text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">
              Today
            </span>
            <span className="block truncate text-sm font-medium tabular-nums">
              {gymFormatter(data.gymTimeZone, { hour: 'numeric', minute: '2-digit' }).format(new Date(data.nextClassToday.startsAt))}
            </span>
            <span className="block truncate text-sm">{data.nextClassToday.classTypeName}</span>
          </span>
        </Link>
      )}

      {/*
        The only thing on this screen that looks forward. Everything else reports what already
        happened — which answers "how am I doing" but never "why open this again". Rendered only when
        the server found something genuinely near; there is deliberately no fallback copy, because a
        manufactured reason to come back is discovered once and never trusted again.
      */}
      {data?.coming && (
        <Card className="rounded-2xl">
          <CardContent className="flex items-center gap-3 py-4">
            <CalendarClock className="size-5 shrink-0 text-primary" />
            <div className="min-w-0">
              <p className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">Coming up</p>
              <p className="truncate font-medium">{data.coming.title}</p>
              <p className="text-sm text-muted-foreground">{data.coming.detail}</p>
            </div>
          </CardContent>
        </Card>
      )}

      {/*
        What the engine noticed, ranked by what can be acted on today — recovery first because it is
        the only one that changes what NOT to do. At most two: a third makes this a report, and
        nobody reads a report before training. Nothing shows when nothing is certain.
      */}
      {data?.insights.map((insight) => (
        <Card
          key={insight.kind}
          className="rounded-2xl border-border edge-light"
          // Volt-tinted so it reads as the one forward-looking thing on a screen of history.
          style={{ background: 'linear-gradient(160deg,#1C1A12,#151517)' }}
        >
          <CardContent className="flex items-start gap-3 py-4">
            <InsightIcon kind={insight.kind} />
            <div className="min-w-0">
              <p className="font-medium">{insight.title}</p>
              <p className="text-sm text-muted-foreground">{insight.detail}</p>
            </div>
          </CardContent>
        </Card>
      ))}

      <WeeklyGoalDialog open={editingGoal} onOpenChange={setEditingGoal} currentGoal={goal} />
    </div>
  )
}
