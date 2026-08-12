import { useMemo, useState, type ReactNode } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { ArrowRight, Clock, TrendingUp } from 'lucide-react'

import { cn } from '@/lib/utils'
import {
  useMyRecovery,
  useMyWorkoutAssignments,
  useMyWorkoutLogs,
  useMyWorkoutSuggestions,
  type MuscleRecovery,
  type MyExerciseSuggestion,
  type MyRecovery,
  type RecoveryStatus,
} from '@/modules/portal/api/portalApi'
import { isStale } from '@/shared/lib/queryTrust'

/**
 * "What should I do today?" — answered at a glance, one-handed, standing on a gym floor.
 *
 * This replaces a screen that rendered about sixty data points at identical visual weight and
 * answered no question at all. A real gym member called it an information bomb, and the inventory is
 * why: eight recovery chips that nearly all read "Fresh", thirteen mastery percentage bars, six
 * equally-weighted suggestions, ten rows of history.
 *
 * The design's central move is that it is almost WORDLESS, and that is the second-order lesson rather
 * than the obvious one. An earlier pass cut the data and replaced it with explanatory sentences,
 * which is no better between sets — prose is slower to parse than a shape. So recovery became a body
 * map, the week became seven cells, and each exercise became one number with an arrow on it. Every
 * sentence lives behind a tap, and the recovery sheet carries all of them verbatim as the accessible
 * equivalent of the map.
 *
 * Mastery is deliberately not fetched here at all. A percentage cannot be acted on mid-session, and
 * it answers "how am I doing over months" — it lives on Progress, one tile away.
 */

// ── Colour, defined once ────────────────────────────────────────────────────────────────────────
// The legend swatches must equal the map fills exactly: a member reading "rested" is matching a
// colour they can see, so the two cannot be allowed to drift. One constant, both consumers.
const ZONE_FILL = {
  today: 'var(--foreground)',
  rested: 'var(--surface-rested)',
  fatigued: 'var(--warning)',
  risk: 'var(--destructive)',
} as const

type ZoneState = keyof typeof ZONE_FILL

/** No zone on the schematic. Not dropped — they appear in the recovery sheet, which is the full list. */
const UNMAPPED_GROUPS = ['Cardio', 'Full Body']

const STATUS_LABEL: Record<RecoveryStatus, string> = {
  Fresh: 'Fresh',
  Ready: 'Ready',
  Fatigued: 'Fatigued',
  OvertrainingRisk: 'Overtraining',
}

const STATUS_COLOR: Record<RecoveryStatus, string> = {
  Fresh: 'var(--foreground)',
  Ready: 'var(--foreground)',
  Fatigued: 'var(--warning)',
  OvertrainingRisk: 'var(--destructive)',
}

// ── Derivation: the logic that matters more than the pixels ─────────────────────────────────────

const UPPER = ['Chest', 'Back', 'Shoulders', 'Arms', 'Core']

/**
 * Two or three words, the largest type on screen, derived from what today's list actually targets —
 * so the headline can never promise a session the rows beneath it do not contain.
 *
 * The plan name is never the headline. "Strength Base" does not tell you what to do this afternoon;
 * "Upper body today" does. The plan is the eyebrow above it.
 */
function headlineFor(status: RecoveryStatus, targetGroups: string[]): string {
  if (status === 'OvertrainingRisk') return 'Rest today'
  if (status === 'Fatigued') return 'Keep it light'
  if (targetGroups.length === 0) return 'First session'

  const hasLegs = targetGroups.includes('Legs')
  const hasUpper = targetGroups.some((g) => UPPER.includes(g))

  if (hasLegs && hasUpper) return 'Full body today'
  if (hasLegs) return 'Legs today'
  return 'Upper body today'
}

const ORDER: Record<MyExerciseSuggestion['suggestion'], number> = {
  ReadyToIncreaseWeight: 0,
  Progressing: 1,
  InsufficientData: 2,
  ConsiderDeload: 3,
}

/**
 * Today's three, and everything held back.
 *
 * Exercises whose muscle group is fatigued are PULLED OUT of the list rather than left in it wearing
 * a contrary label. That is the mechanism that stops the screen contradicting itself — the old one
 * showed "you're fully recovered and ready for a strong session" directly above two rows reading
 * "consider a lighter session". Here the amber zone on the map is the explanation, and the words are
 * in the sheet.
 */
function splitSuggestions(suggestions: MyExerciseSuggestion[], recovery: MyRecovery | undefined) {
  const groupStatus = new Map((recovery?.muscleGroups ?? []).map((m) => [m.muscleGroup, m.status]))
  const isBlocked = (s: MyExerciseSuggestion) => {
    const st = s.muscleGroup ? groupStatus.get(s.muscleGroup) : undefined
    return st === 'Fatigued' || st === 'OvertrainingRisk'
  }

  const eligible = suggestions.filter((s) => !isBlocked(s)).sort((a, b) => ORDER[a.suggestion] - ORDER[b.suggestion])
  const blocked = suggestions.filter(isBlocked)

  // At overtraining risk the honest answer is that nothing is ready. Everything moves behind the
  // reveal rather than being softened into a list the member should not be acting on at all.
  if (recovery?.status === 'OvertrainingRisk') {
    return { today: [] as MyExerciseSuggestion[], rest: suggestions, blocked }
  }

  return { today: eligible.slice(0, 3), rest: [...eligible.slice(3), ...blocked], blocked }
}

type Target = { arrow: string; value: string; unit: string; tone: string }

/**
 * One number, one arrow, one unit.
 *
 * Bodyweight exercises never render a weight — no "0 kg", no "— kg", no empty slot. The old screen
 * printed "Last: — kg × 33 reps" for a plank and "best 0kg" for a push-up, which reads as broken
 * data. Their target is a rep count, because reps are the thing they can actually beat.
 */
function targetFor(s: MyExerciseSuggestion, blocked: boolean, lighten: boolean): Target {
  if (blocked) return { arrow: '—', value: '—', unit: 'not today', tone: 'var(--warning)' }

  const bodyweight = s.lastWeightKg === null
  const reps = `${s.lastTotalReps ?? 0}+`

  switch (s.suggestion) {
    case 'ReadyToIncreaseWeight':
      // A fatigued member is never told to add weight, whatever the plateau rule says in isolation.
      if (lighten) return { arrow: '→', value: bodyweight ? reps : `${s.lastWeightKg}`, unit: bodyweight ? 'reps' : 'kg', tone: 'var(--foreground)' }
      if (bodyweight) return { arrow: '↑', value: reps, unit: 'reps', tone: 'var(--foreground)' }
      return { arrow: '↑', value: `${s.suggestedNextWeightKg}`, unit: 'kg', tone: 'var(--foreground)' }
    case 'ConsiderDeload':
      /*
       * Last time's load, stated as last time's load — never an invented target.
       *
       * This branch read suggestedNextWeightKg and fell back to an em-dash, and the fallback was not
       * an edge case: GetMyWorkoutSuggestionsQuery populates that field ONLY for
       * ReadyToIncreaseWeight, so a ConsiderDeload row was structurally guaranteed to print "— kg"
       * under a column headed "Target". The seed hides it because the local ordering buries this
       * verdict; a real member with three logged movements meets it immediately.
       *
       * Saying "you came down to 55" is true and useful. Naming a number to aim for after a drop
       * would be the app inventing coaching it has no basis for — the policy's own comment calls
       * this its noisiest verdict, raised by ordinary session-to-session variation.
       */
      return bodyweight
        ? { arrow: '↓', value: reps, unit: 'reps', tone: 'var(--warning)' }
        : { arrow: '↓', value: `${s.lastWeightKg ?? '—'}`, unit: 'kg', tone: 'var(--warning)' }
    case 'Progressing':
      return bodyweight
        ? { arrow: '↑', value: reps, unit: 'reps', tone: 'var(--foreground)' }
        : { arrow: '↑', value: `${s.lastWeightKg}`, unit: 'kg', tone: 'var(--foreground)' }
    default:
      return { arrow: '—', value: '—', unit: '1 session', tone: 'var(--muted-foreground)' }
  }
}

/** "60 kg · 24 reps" or "bodyweight · 32 reps". Reps are session totals, said once in the footer. */
function microLine(s: MyExerciseSuggestion): string {
  const reps = `${s.lastTotalReps ?? 0} reps`
  return s.lastWeightKg === null ? `bodyweight · ${reps}` : `${s.lastWeightKg} kg · ${reps}`
}

const WEEKDAY = ['S', 'M', 'T', 'W', 'T', 'F', 'S']

/**
 * Seven cells, oldest to newest, derived from the SAME session dates the history sheet lists.
 * Deliberately not computed from SessionsLast7Days: two independent derivations of "this week" is
 * exactly how a strip and a sheet end up disagreeing in front of the member.
 */
function buildWeek(sessionDates: string[]) {
  const trained = new Set(sessionDates.map((d) => new Date(d).toDateString()))
  const today = new Date()
  const days: { letter: string; trained: boolean; isToday: boolean }[] = []

  for (let i = 6; i >= 0; i--) {
    const d = new Date(today)
    d.setDate(today.getDate() - i)
    days.push({ letter: WEEKDAY[d.getDay()], trained: trained.has(d.toDateString()), isToday: i === 0 })
  }
  return days
}

// ── Pieces ──────────────────────────────────────────────────────────────────────────────────────

function BodyMap({ zones, ghost = false, className }: { zones: Record<string, ZoneState>; ghost?: boolean; className?: string }) {
  // stroke = the card colour, and it is load-bearing: it is what keeps two adjacent same-coloured
  // zones legible as separate shapes at 84px. Without it they fuse into a single mass.
  const common = ghost
    ? { fill: 'none', stroke: 'var(--border-ghost)', strokeWidth: 1.6, strokeDasharray: '3 3' }
    : { stroke: 'var(--card)', strokeWidth: 2.4 }

  const fill = (zone: string) => (ghost ? 'none' : ZONE_FILL[zones[zone] ?? 'rested'])

  return (
    <svg viewBox="0 0 100 116" className={className} aria-hidden>
      <circle cx={50} cy={11} r={8} fill={ghost ? 'none' : 'var(--border-chip)'} {...common} />
      <rect x={14} y={24} width={20} height={12} rx={6} fill={fill('shoulders')} {...common} />
      <rect x={66} y={24} width={20} height={12} rx={6} fill={fill('shoulders')} {...common} />
      <rect x={36} y={24} width={28} height={13} rx={6} fill={fill('chest')} {...common} />
      <rect x={36} y={39} width={28} height={9} rx={4} fill={fill('back')} {...common} />
      <rect x={14} y={39} width={13} height={30} rx={6} fill={fill('arms')} {...common} />
      <rect x={73} y={39} width={13} height={30} rx={6} fill={fill('arms')} {...common} />
      <rect x={38} y={50} width={24} height={15} rx={6} fill={fill('core')} {...common} />
      <rect x={36} y={68} width={12} height={42} rx={6} fill={fill('legs')} {...common} />
      <rect x={52} y={68} width={12} height={42} rx={6} fill={fill('legs')} {...common} />
    </svg>
  )
}

function Legend() {
  const items: [ZoneState, string][] = [
    ['today', 'today'],
    ['rested', 'rested'],
    ['fatigued', 'not recovered'],
  ]
  return (
    <div className="mt-[14px] flex gap-3">
      {items.map(([state, label]) => (
        <span key={label} className="flex items-center gap-1.5">
          <span className="size-[7px] rounded-[2px]" style={{ background: ZONE_FILL[state] }} />
          <span className="font-display text-[10.5px] font-semibold tracking-[0.04em] text-muted-foreground uppercase">
            {label}
          </span>
        </span>
      ))}
    </div>
  )
}

function DayStrip({ days }: { days: ReturnType<typeof buildWeek> }) {
  return (
    <div className="mt-4 flex gap-[5px]">
      {days.map((d, i) => (
        <span
          key={i}
          className={cn(
            'flex h-[34px] flex-1 items-center justify-center rounded-[9px] border-[1.5px] font-display text-xs font-bold lg:h-[38px]',
            d.trained ? 'bg-foreground text-background' : 'bg-surface-cell text-muted-foreground',
            d.isToday ? 'border-muted-foreground' : 'border-transparent',
          )}
        >
          {d.letter}
        </span>
      ))}
    </div>
  )
}

function ExerciseRow({ s, blocked, lighten }: { s: MyExerciseSuggestion; blocked: boolean; lighten: boolean }) {
  const t = targetFor(s, blocked, lighten)
  return (
    <li
      className={cn(
        'flex items-center justify-between gap-3 rounded-[18px] px-4 py-[14px] lg:px-5 lg:py-4',
        blocked ? 'border border-dashed border-border bg-surface-dimmed opacity-[.72]' : 'border border-border bg-card',
      )}
    >
      <span className="min-w-0">
        <span className="block truncate text-base font-semibold tracking-[-0.01em] lg:text-[17px]">{s.exerciseName}</span>
        <span className="mt-[3px] block text-xs font-medium text-muted-foreground tabular-nums">{microLine(s)}</span>
      </span>
      <span className="flex shrink-0 items-baseline gap-1">
        <span className="font-display text-[15px] font-bold" style={{ color: t.tone }}>
          {t.arrow}
        </span>
        <span
          className={cn(
            'font-display font-extrabold tracking-[-0.03em] tabular-nums',
            blocked ? 'text-[22px]' : 'text-[27px] lg:text-[30px]',
          )}
          style={{ color: t.tone }}
        >
          {t.value}
        </span>
        <span className="text-[11.5px] font-semibold text-muted-foreground">{t.unit}</span>
      </span>
    </li>
  )
}

function Sheet({ title, onClose, children }: { title: string; onClose: () => void; children: ReactNode }) {
  return (
    <div className="fixed inset-0 z-50 flex items-end" role="dialog" aria-modal="true" aria-label={title}>
      <button type="button" className="absolute inset-0 bg-black/[.66] backdrop-blur-[2px]" aria-label="Close" onClick={onClose} />
      <div className="relative max-h-[80%] w-full overflow-y-auto rounded-t-[26px] rounded-b-[46px] border-t border-border-outline bg-card px-[18px] pt-[14px] pb-[30px]">
        <span className="mx-auto mb-3 block h-1 w-[38px] rounded-full bg-border-outline" />
        <p className="font-display text-lg font-extrabold tracking-tight">{title}</p>
        {children}
      </div>
    </div>
  )
}

// ── Screen ──────────────────────────────────────────────────────────────────────────────────────

const dateFmt = new Intl.DateTimeFormat('en-US', { weekday: 'long', day: 'numeric', month: 'long' })
const shortFmt = new Intl.DateTimeFormat('en-US', { day: 'numeric', month: 'short', year: 'numeric' })

export default function MyTrainingPage() {
  const navigate = useNavigate()
  const recovery = useMyRecovery()
  const suggestions = useMyWorkoutSuggestions()
  const assignments = useMyWorkoutAssignments()
  const workouts = useMyWorkoutLogs()

  const [sheet, setSheet] = useState<null | 'recovery' | 'history'>(null)
  const [expanded, setExpanded] = useState(false)

  const rec = recovery.data
  const status = rec?.status ?? 'Fresh'
  const { today, rest, blocked } = useMemo(() => splitSuggestions(suggestions.data ?? [], rec), [suggestions.data, rec])

  const targetGroups = useMemo(
    () => [...new Set(today.map((s) => s.muscleGroup).filter((g): g is string => !!g))],
    [today],
  )

  const sessions = workouts.data ?? []
  const week = useMemo(() => buildWeek(sessions.map((w) => w.loggedAt)), [sessions])

  // Fill precedence, highest first: risk → fatigued → in today's list → rested.
  const zones = useMemo(() => {
    const z: Record<string, ZoneState> = {}
    const key = (g: string) => g.toLowerCase().replace(/\s/g, '')
    for (const m of rec?.muscleGroups ?? []) {
      if (UNMAPPED_GROUPS.includes(m.muscleGroup)) continue
      z[key(m.muscleGroup)] = m.status === 'OvertrainingRisk' ? 'risk' : m.status === 'Fatigued' ? 'fatigued' : 'rested'
    }
    for (const g of targetGroups) {
      const k = key(g)
      if (z[k] !== 'risk' && z[k] !== 'fatigued') z[k] = 'today'
    }
    return z
  }, [rec, targetGroups])

  const plan = assignments.data?.[0]

  /*
   * "Never trained" and "could not load" are the same empty array, and telling them apart is the
   * whole difference between an honest screen and an insulting one.
   *
   * Caught by the API being down during live verification: every query failed, sessions came back
   * empty, and this screen cheerfully told a member with thirty-eight logged sessions that it was
   * their first. The week-one state is the most confident thing here — dashed placeholders, "Week 1",
   * "targets appear after your first session" — so it is the worst possible thing to show on a
   * dropped connection.
   *
   * Week one is therefore claimed only when the request SUCCEEDED and came back empty. On failure the
   * shell stays, the button stays live — a member should still be able to start a session when
   * recovery fails to load — and one quiet line says the data is missing rather than inventing a
   * beginner.
   */
  const failed = isStale(workouts) || isStale(recovery) || isStale(suggestions)
  const isNewMember = !failed && !workouts.isLoading && sessions.length === 0
  const lighten = status === 'Fatigued'
  const headline = failed ? 'Train today' : isNewMember ? 'First session' : headlineFor(status, targetGroups)

  return (
    /*
      Centred on the same axis as every other member screen, and as the tab bar.

      This was `max-w-[1064px] ... lg:grid-cols-[660px_1fr]`, while every other member page is
      `max-w-2xl` (672px) and MemberTabBar is `mx-auto ... max-w-2xl`. The arithmetic: at 1024px+ the
      1064 container less lg:px-10 gives a 984 content box, of which column one took a FIXED 660 —
      so the hero card's centre line landed ~202px left of the viewport centre the tab bar's centre
      button sits on, and moving Home -> Train visibly shoved the page sideways. Between 768 and
      1023px `lg:grid` had not engaged at all, so a 900px window rendered an 836px column against
      672px everywhere else.

      `minmax(0,1fr)` rather than a fixed track is what lets the page re-centre when the reference
      rail is absent — which is every new member and every failed fetch, the case that looked worst.

      px-4 is gone because AppShell already applies p-4 to the member <main>; the two were
      double-padding to 32px on mobile.
    */
    <div className="mx-auto w-full max-w-2xl pt-8 pb-32 lg:grid lg:max-w-[1024px] lg:grid-cols-[minmax(0,1fr)_300px] lg:items-start lg:gap-6 lg:pt-[34px] lg:pb-[110px]">
      <div>
        <div className="mb-[14px] flex items-center justify-between px-0.5">
          <span className="font-display text-[11.5px] font-bold tracking-[0.12em] text-muted-foreground uppercase">
            {dateFmt.format(new Date())}
          </span>
          <span
            className={cn(
              'flex items-center gap-1.5 rounded-full border py-1 pr-2.5 pl-2',
              isNewMember ? 'border-border-quiet bg-foreground/[0.04]' : 'border-border-chip bg-foreground/[0.06]',
            )}
          >
            <span
              className="size-1.5 rounded-full"
              style={{ background: isNewMember ? 'var(--border-outline)' : STATUS_COLOR[status] }}
            />
            <span
              className="font-display text-[11.5px] font-bold"
              style={{ color: isNewMember ? 'var(--muted-foreground)' : STATUS_COLOR[status] }}
            >
              {isNewMember ? 'Week 1' : STATUS_LABEL[status]}
            </span>
          </span>
        </div>

        <div className="edge-light rounded-[26px] border border-border bg-card p-[18px] lg:p-[26px]">
          <div className="flex items-center gap-[14px] lg:gap-[26px]">
            <div className="min-w-0 flex-1">
              <p className="font-display text-[10.5px] font-bold tracking-[0.1em] text-muted-foreground uppercase">
                {plan ? plan.workoutTemplateName : 'No plan'}
              </p>
              <h1 className="mt-[9px] font-display text-[37px] leading-none font-extrabold tracking-[-0.035em] text-balance lg:text-[52px] lg:leading-[0.98] lg:tracking-[-0.04em]">
                {headline}
              </h1>
            </div>
            <button
              type="button"
              aria-label="Recovery by muscle group"
              onClick={() => rec && setSheet('recovery')}
              className="press shrink-0"
            >
              <BodyMap zones={zones} ghost={isNewMember} className="h-[97px] w-[84px] lg:h-[153px] lg:w-[132px]" />
            </button>
          </div>
          <Legend />
          <DayStrip days={week} />
        </div>

        <div className="mt-[14px]">
          <div className="flex items-center justify-between px-1 pb-[9px]">
            <span className="font-display text-[10.5px] font-bold tracking-[0.11em] text-muted-foreground uppercase">Today</span>
            {/* "Last", not "Target": only ReadyToIncreaseWeight yields a number to aim for. The other
                three verdicts report what the member already did, and heading them "Target" told
                members to lift a weight the app had not chosen. */}
            <span className="font-display text-[10.5px] font-bold tracking-[0.11em] text-muted-foreground uppercase">Last</span>
          </div>

          {failed ? (
            /* Not "Nothing ready" — that sentence claims every muscle group needs recovery, which is
               a statement about the member rather than about the connection. Say what is true. */
            <div className="rounded-[18px] border border-border bg-card px-4 py-5 text-center">
              <p className="text-[15px] font-semibold">Couldn't load today's targets</p>
              <button
                type="button"
                onClick={() => {
                  void suggestions.refetch()
                  void recovery.refetch()
                  void workouts.refetch()
                }}
                className="mt-2 text-[13px] font-semibold text-primary underline underline-offset-4"
              >
                Try again
              </button>
            </div>
          ) : isNewMember ? (
            <>
              <ul className="space-y-2">
                {[96, 72, 84].map((w, i) => (
                  <li
                    key={i}
                    className="flex h-[62px] items-center justify-between rounded-[18px] border border-dashed border-border bg-surface-recessed px-4"
                  >
                    <span className="block h-[11px] rounded bg-surface-cell" style={{ width: w }} />
                    <span className="text-border-chip">—</span>
                  </li>
                ))}
              </ul>
              <p className="mt-3 text-center font-display text-[11px] font-semibold tracking-wide text-muted-foreground uppercase">
                targets appear after your first session
              </p>
            </>
          ) : today.length === 0 ? (
            <div className="rounded-[18px] border border-border bg-card px-4 py-5 text-center">
              <p className="text-[15px] font-semibold">Nothing ready</p>
              <p className="mt-1 text-[13px] text-muted-foreground">
                Every muscle group needs more recovery than it has had.
              </p>
            </div>
          ) : (
            <ul className="space-y-2">
              {today.map((s) => (
                <ExerciseRow key={s.exerciseId} s={s} blocked={false} lighten={lighten} />
              ))}
              {expanded &&
                rest.map((s) => <ExerciseRow key={s.exerciseId} s={s} blocked={blocked.includes(s)} lighten={lighten} />)}
            </ul>
          )}

          {!isNewMember && (today.length > 0 || rest.length > 0) && (
            <div className="flex items-center justify-between px-1 pt-[11px]">
              <span className="font-display text-[10.5px] font-semibold tracking-wide text-muted-foreground uppercase">
                reps are session totals
              </span>
              {rest.length > 0 && (
                <button
                  type="button"
                  onClick={() => setExpanded((v) => !v)}
                  className="font-display text-[12.5px] font-bold text-muted-foreground"
                >
                  {expanded ? `Hide the other ${rest.length}` : `+${rest.length}`}
                </button>
              )}
            </div>
          )}
        </div>

        {/*
          The volt button DISAPPEARS at overtraining risk, replaced by a quiet outline. Reserving the
          accent for one thing is the whole reason it carries weight, and keeping it green while the
          honest answer is "rest" would spend that on the one screen where it should not be spent.
        */}
        <button
          type="button"
          onClick={() => navigate('/workout')}
          className={cn(
            'press mt-4 flex h-[60px] w-full items-center justify-center gap-2 rounded-[18px] lg:w-auto lg:px-[38px]',
            status === 'OvertrainingRisk'
              ? 'border border-border-outline font-display text-[17px] font-bold text-body-copy'
              : 'shadow-volt bg-primary font-display text-[18px] font-extrabold tracking-[-0.015em] text-primary-foreground',
          )}
        >
          {status === 'OvertrainingRisk'
            ? 'Train anyway'
            : lighten
              ? 'Start a lighter session'
              : isNewMember
                ? 'Start'
                : 'Start session'}
          <ArrowRight className="size-[19px]" />
        </button>

        {/* Absent for a new member — there is nothing behind them yet. */}
        {!isNewMember && (
          <div className="mt-3 flex gap-2.5 lg:hidden">
            <button
              type="button"
              onClick={() => setSheet('history')}
              className="press flex-1 rounded-[18px] border border-border-quiet bg-surface-recessed p-[14px] text-left"
            >
              <Clock className="size-[19px] text-muted-foreground" />
              <span className="mt-2 block font-display text-[19px] font-extrabold">{sessions.length}</span>
              <span className="block text-[11px] font-semibold text-muted-foreground uppercase">sessions</span>
            </button>
            <Link
              to="/my-progress"
              className="press flex-1 rounded-[18px] border border-border-quiet bg-surface-recessed p-[14px]"
            >
              <TrendingUp className="size-[19px] text-muted-foreground" />
              <span className="mt-2 block text-[13.5px] font-semibold">Progress</span>
            </Link>
          </div>
        )}
      </div>

      {/* Desktop reference rail. The two sheets become visible panels, because a sheet on desktop is
          a modal for something that could simply be on screen. The hero keeps its width so nothing
          gets promoted merely because room appeared. */}
      <aside className="hidden lg:block">
        {rec && (
          <div className="rounded-[22px] border border-border-quiet bg-surface-recessed p-[18px]">
            <p className="font-display text-[10.5px] font-bold tracking-[0.11em] text-muted-foreground uppercase">Recovery</p>
            <ul className="mt-3 space-y-2">
              {rec.muscleGroups.map((m) => (
                <li key={m.muscleGroup} className="flex items-center justify-between gap-2 text-sm">
                  <span className="flex items-center gap-2">
                    <span className="size-[7px] rounded-full" style={{ background: STATUS_COLOR[m.status] }} />
                    {m.muscleGroup}
                  </span>
                  <span className="text-xs font-semibold" style={{ color: STATUS_COLOR[m.status] }}>
                    {STATUS_LABEL[m.status]}
                  </span>
                </li>
              ))}
            </ul>
          </div>
        )}
        {sessions.length > 0 && (
          <div className="mt-4 rounded-[22px] border border-border-quiet bg-surface-recessed p-[18px]">
            <p className="font-display text-[10.5px] font-bold tracking-[0.11em] text-muted-foreground uppercase">
              Recent · {sessions.length}
            </p>
            <ul className="mt-3 space-y-2">
              {sessions.slice(0, 6).map((w) => (
                <li key={w.id} className="flex items-center justify-between gap-2 text-sm">
                  <span className="truncate">{w.workoutTemplateName ?? w.character}</span>
                  <span className="shrink-0 text-xs text-muted-foreground tabular-nums">
                    {shortFmt.format(new Date(w.loggedAt))}
                  </span>
                </li>
              ))}
            </ul>
          </div>
        )}
      </aside>

      {sheet === 'recovery' && rec && (
        <Sheet title={STATUS_LABEL[rec.status]} onClose={() => setSheet(null)}>
          {/* The Reason string verbatim. This sheet is the map's text equivalent, so it must not
              paraphrase what the map is showing. */}
          <p className="mt-2 text-sm leading-relaxed text-body-copy">{rec.reason}</p>
          <ul className="mt-4 space-y-3">
            {rec.muscleGroups.map((m: MuscleRecovery) => (
              <li key={m.muscleGroup} className="flex items-start justify-between gap-3">
                <span className="flex min-w-0 items-start gap-2">
                  <span className="mt-[6px] size-[7px] shrink-0 rounded-full" style={{ background: STATUS_COLOR[m.status] }} />
                  <span className="min-w-0">
                    <span className="block text-[15px] font-semibold">{m.muscleGroup}</span>
                    <span className="block text-[12.5px] text-muted-foreground">{m.reason}</span>
                  </span>
                </span>
                <span className="shrink-0 text-[12.5px] font-semibold" style={{ color: STATUS_COLOR[m.status] }}>
                  {STATUS_LABEL[m.status]}
                </span>
              </li>
            ))}
          </ul>
        </Sheet>
      )}

      {sheet === 'history' && (
        <Sheet title={`${sessions.length} sessions`} onClose={() => setSheet(null)}>
          <ul className="mt-3 space-y-3">
            {sessions.slice(0, 10).map((w) => (
              <li key={w.id} className="flex items-center justify-between gap-3">
                <span className="truncate text-[15px] font-semibold">{w.workoutTemplateName ?? w.character}</span>
                <span className="shrink-0 text-[13px] text-muted-foreground tabular-nums">
                  {shortFmt.format(new Date(w.loggedAt))}
                </span>
              </li>
            ))}
          </ul>
        </Sheet>
      )}
    </div>
  )
}
