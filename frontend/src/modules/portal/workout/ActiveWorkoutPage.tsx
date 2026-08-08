import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { ArrowUp, Check, Dumbbell, Loader2, Plus, X } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'
import { WorkoutCelebration } from '@/modules/portal/components/WorkoutCelebration'
import {
  SESSION_SOURCE_LABEL,
  useLogMyWorkout,
  useMyNextSession,
  useMyPersonalRecords,
  type MyWorkoutResult,
} from '@/modules/portal/api/portalApi'
import {
  REST_SECONDS,
  completedEntries,
  firstUnfinishedSet,
  useActiveWorkout,
  type ActiveExercise,
} from '@/modules/portal/workout/activeWorkoutStore'

/** Ticks once a second so every derived clock on this screen re-reads the wall clock. */
function useNow(active: boolean): number {
  const [now, setNow] = useState(() => Date.now())
  useEffect(() => {
    if (!active) return
    const id = window.setInterval(() => setNow(Date.now()), 1000)
    return () => window.clearInterval(id)
  }, [active])
  return now
}

function clock(totalSeconds: number): string {
  const safe = Math.max(0, Math.floor(totalSeconds))
  const minutes = Math.floor(safe / 60)
  const seconds = safe % 60
  return `${minutes}:${String(seconds).padStart(2, '0')}`
}

/**
 * One row of the set table. The set being worked on is the only one that looks editable, because it
 * is the only one that is: finished sets are the record of what happened and are read back dimmed.
 */
function SetRow({
  index,
  weightKg,
  reps,
  done,
  active,
  onChange,
  onComplete,
}: {
  index: number
  weightKg: number | null
  reps: number
  done: boolean
  active: boolean
  onChange: (patch: { weightKg?: number | null; reps?: number }) => void
  onComplete: () => void
}) {
  return (
    // Three states that must be told apart at a glance, mid-set, at arm's length. Done is dimmed
    // because it is history; upcoming keeps full-contrast text because it is still to do; only the
    // active row is outlined. Done and upcoming previously differed by a background one step apart on
    // a dark surface, which made a set the member had not started look like one they had finished.
    //
    // The active row now carries three signals at once — a 1.5px volt border, a 4px halo and a
    // coloured drop shadow — because one of them alone is a border, and a border is what every other
    // row already has. The width is 1.5px on ALL three states so the stack never reflows by a
    // half-pixel as the active row moves down it.
    <div
      className={cn(
        'flex items-center gap-2 rounded-2xl border-[1.5px] p-2 transition-all duration-[240ms] ease-[var(--ease-uplift)]',
        done && 'border-transparent bg-muted opacity-60',
        active &&
          'border-primary bg-accent shadow-[0_0_0_4px_rgb(214_249_74_/_0.10),0_10px_24px_-10px_rgb(214_249_74_/_0.45)]',
        !done && !active && 'border-border',
      )}
    >
      <span
        className={cn(
          'w-7 shrink-0 text-center font-display font-black tabular-nums',
          active ? 'text-primary' : 'text-muted-foreground',
        )}
      >
        {index + 1}
      </span>

      <label className="sr-only" htmlFor={`set-${index}-weight`}>
        Set {index + 1} weight in kilograms
      </label>
      <input
        id={`set-${index}-weight`}
        type="number"
        inputMode="decimal"
        step="0.5"
        min={0}
        disabled={done}
        value={weightKg ?? ''}
        placeholder="—"
        onChange={(e) => onChange({ weightKg: e.target.value === '' ? null : Number(e.target.value) })}
        className={cn(
          // 21px/800 on an ink ground: this is read at arm's length, one-handed, mid-set. The active
          // row's fields take a dim olive border rather than a volt one — the row itself is already
          // outlined in volt, and a second volt line inside it competes with the first.
          'h-12 min-w-0 flex-1 rounded-xl border bg-background px-3 text-center font-display text-[21px] font-extrabold tabular-nums outline-none',
          active ? 'border-[#3A3E1F] text-foreground' : 'border-transparent',
          done ? 'bg-transparent text-muted-foreground' : 'text-foreground',
        )}
      />

      <label className="sr-only" htmlFor={`set-${index}-reps`}>
        Set {index + 1} reps
      </label>
      <input
        id={`set-${index}-reps`}
        type="number"
        inputMode="numeric"
        min={1}
        disabled={done}
        value={reps || ''}
        onChange={(e) => onChange({ reps: Number(e.target.value) })}
        className={cn(
          'h-12 min-w-0 flex-1 rounded-xl border bg-background px-3 text-center font-display text-[21px] font-extrabold tabular-nums outline-none',
          active ? 'border-[#3A3E1F] text-foreground' : 'border-transparent',
          done ? 'bg-transparent text-muted-foreground' : 'text-foreground',
        )}
      />

      <button
        type="button"
        aria-label={done ? `Set ${index + 1} logged` : `Log set ${index + 1}`}
        disabled={done || !active}
        onClick={onComplete}
        className={cn(
          // Filled at every stage, never an outline. This is the most-tapped control in the product
          // and it is tapped without looking; an empty circle asks the member to find an edge.
          // The transition is spelled out rather than using `press`, which would replace the colour
          // transition with a transform-only one and make the fill snap on log.
          'flex size-12 shrink-0 items-center justify-center rounded-xl transition-[transform,background-color,color] duration-[120ms] ease-[var(--ease-uplift)] active:scale-[.97]',
          done ? 'bg-success text-success-foreground' : active ? 'bg-primary text-primary-foreground shadow-volt' : 'bg-muted',
        )}
      >
        {done && <Check className="size-5" />}
        {!done && active && <Check className="size-5" />}
      </button>
    </div>
  )
}

/**
 * The live session: what the member is doing right now, set by set, while they are still in the gym.
 *
 * It deliberately does NOT replace the home screen's one-tap confirmation, which stays the primary
 * path and the product's whole thesis — after a hard session nobody wants a second job, and most
 * members will always want to say "I did that" and be done. This is the other member: the one who is
 * mid-session, resting between sets with a phone already in their hand, for whom typing the set they
 * just did costs nothing and remembering it two hours later costs everything.
 *
 * Both paths write through the same /api/me/workouts endpoint, so a session logged either way sets the
 * same records, moves the same mastery and closes the same ring.
 */
export default function ActiveWorkoutPage() {
  const navigate = useNavigate()
  const proposal = useMyNextSession()
  const records = useMyPersonalRecords()
  const logWorkout = useLogMyWorkout()
  const [celebration, setCelebration] = useState<MyWorkoutResult | null>(null)

  const { startedAt, sourceLabel, exercises, currentIndex, restEndsAt, start, abandon, editSet, completeSet, addSet, goToExercise, skipRest } =
    useActiveWorkout()

  const running = startedAt !== null
  const now = useNow(running)

  /**
   * Seed the session from the server's proposal the first time this screen opens, and never again
   * while one is running — a member who navigates away mid-session and returns must find the sets
   * they logged, not a fresh copy of the plan.
   */
  useEffect(() => {
    if (running || !proposal.data?.canConfirm) return
    const seeded: ActiveExercise[] = proposal.data.entries.map((e) => ({
      exerciseId: e.exerciseId,
      exerciseName: e.exerciseName,
      // The proposal says "3 sets of 8 at 60kg"; the session turns that into three individual sets
      // the member confirms one at a time, each free to differ from the plan.
      sets: Array.from({ length: Math.max(1, e.sets) }, () => ({
        weightKg: e.weightKg,
        reps: e.reps,
        done: false,
      })),
    }))
    start(SESSION_SOURCE_LABEL[proposal.data.source] || 'Your session', seeded)
  }, [running, proposal.data, start])

  const exercise = exercises[currentIndex]
  const activeSetIndex = firstUnfinishedSet(exercise)
  const entries = completedEntries(exercises)
  const resting = restEndsAt !== null && restEndsAt > now
  const restRemaining = resting ? (restEndsAt - now) / 1000 : 0

  /**
   * The member's current heaviest single on this movement. Only a MaxWeight record can say whether the
   * set in front of them is worth anything — a session-volume best says nothing about one set — and
   * the callout appears only when the weight they have actually typed would beat it. No record on file
   * means every set is a first, which is not news worth a banner.
   */
  const currentBest = exercise
    ? (records.data ?? [])
        .filter((r) => r.exerciseId === exercise.exerciseId && r.type === 'MaxWeight')
        .reduce<number | null>((best, r) => (best === null || r.value > best ? r.value : best), null)
    : null
  const plannedWeight = activeSetIndex !== null ? exercise?.sets[activeSetIndex]?.weightKg ?? null : null
  const prInReach = currentBest !== null && plannedWeight !== null && plannedWeight > currentBest

  const finish = () => {
    if (entries.length === 0) {
      toast.error('Log at least one set first.')
      return
    }
    logWorkout.mutate(entries, {
      onSuccess: (result) => {
        setCelebration(result)
        abandon()
      },
      onError: () => toast.error("Couldn't save that session."),
    })
  }

  const quit = () => {
    abandon()
    navigate('/portal')
  }

  if (proposal.isLoading && !running) {
    return <Skeleton className="h-96 w-full rounded-3xl shimmer" />
  }

  // Nothing to propose — no plan, no history, no catalogue. The manual logger is the honest fallback
  // rather than an empty session with nothing in it to tick off.
  if (!running) {
    return (
      <div className="mx-auto max-w-2xl space-y-4 text-center">
        <Dumbbell className="mx-auto size-10 text-muted-foreground" />
        <h1 className="font-display text-2xl font-black tracking-tight">Nothing to start yet</h1>
        <p className="text-sm text-muted-foreground">
          Once you've logged a session or your trainer sets you a plan, this becomes one tap.
        </p>
        <Button asChild className="press h-12 w-full rounded-2xl shadow-volt">
          <Link to="/log-activity">Log a workout manually</Link>
        </Button>
      </div>
    )
  }

  return (
    <div className="mx-auto max-w-2xl pb-40">
      <div className="flex items-center gap-3">
        <button
          type="button"
          onClick={quit}
          aria-label="End session without saving"
          className="press flex size-11 shrink-0 items-center justify-center rounded-2xl bg-muted text-muted-foreground edge-light hover:text-foreground"
        >
          <X className="size-5" />
        </button>
        <div className="min-w-0 flex-1">
          <p className="truncate font-display text-lg font-black tracking-tight">{sourceLabel}</p>
          <p className="text-xs text-muted-foreground">
            Exercise {currentIndex + 1} of {exercises.length}
          </p>
        </div>
        {/* Elapsed since the session began — derived from a stored start time, so it stays right
            through a lock screen, a backgrounded tab and a reload. */}
        <span className="flex shrink-0 items-center gap-2 rounded-xl bg-muted px-3 py-2 edge-light">
          <span className="size-2 animate-pulse rounded-full bg-primary shadow-[0_0_8px_rgb(214_249_74_/_0.7)]" />
          <span className="font-display font-bold tabular-nums">{clock((now - startedAt!) / 1000)}</span>
        </span>
      </div>

      {/* One segment per exercise: filled once every set in it is done. */}
      <ul className="mt-3 flex gap-1.5">
        {exercises.map((ex, i) => {
          const complete = ex.sets.every((s) => s.done)
          return (
            <li key={ex.exerciseId + i} className="flex-1">
              <button
                type="button"
                onClick={() => goToExercise(i)}
                aria-label={`Go to ${ex.exerciseName}`}
                aria-current={i === currentIndex ? 'step' : undefined}
                // Three states, not two: done, being worked, still ahead. The glow is on the current
                // segment alone — a segment that is merely finished has nothing left to say.
                className={cn(
                  'h-1.5 w-full rounded-full transition-colors',
                  complete
                    ? 'bg-primary'
                    : i === currentIndex
                      ? 'bg-primary/40 shadow-[0_0_10px_rgb(214_249_74_/_0.45)]'
                      : 'bg-border',
                )}
              />
            </li>
          )
        })}
      </ul>

      {exercise && (
        <>
          <h1 className="mt-5 font-display text-3xl leading-tight font-black tracking-tight">
            {exercise.exerciseName}
          </h1>
          {currentBest !== null && (
            <p className="mt-1 text-sm text-muted-foreground">
              Your best: {currentBest}kg
            </p>
          )}

          {prInReach && (
            <div className="mt-3 flex items-center gap-2 rounded-2xl border border-warning/40 bg-warning/10 p-3 edge-light">
              <ArrowUp className="size-5 shrink-0 text-warning" />
              <p className="text-sm font-medium text-warning">
                Beat {currentBest}kg to set a personal record.
              </p>
            </div>
          )}

          <div className="mt-4 flex items-center gap-2 px-2 text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">
            <span className="w-7 shrink-0 text-center">Set</span>
            <span className="flex-1 text-center">Kg</span>
            <span className="flex-1 text-center">Reps</span>
            <span className="size-12 shrink-0" />
          </div>

          <div className="mt-1.5 space-y-2">
            {exercise.sets.map((s, i) => (
              <SetRow
                key={i}
                index={i}
                weightKg={s.weightKg}
                reps={s.reps}
                done={s.done}
                active={i === activeSetIndex}
                onChange={(patch) => editSet(currentIndex, i, patch)}
                onComplete={() => completeSet(currentIndex, i)}
              />
            ))}

            <button
              type="button"
              onClick={() => addSet(currentIndex)}
              className="press flex h-12 w-full items-center justify-center gap-2 rounded-2xl border border-dashed border-border text-sm font-medium text-muted-foreground hover:text-foreground"
            >
              <Plus className="size-4" />
              Add set
            </button>
          </div>

          {/* Once every set is ticked off, moving on is the obvious next thing rather than something
              the member has to find. The last exercise offers the finish instead. */}
          {activeSetIndex === null && currentIndex < exercises.length - 1 && (
            <Button
              className="press mt-4 h-12 w-full rounded-2xl shadow-volt"
              onClick={() => goToExercise(currentIndex + 1)}
            >
              Next: {exercises[currentIndex + 1].exerciseName}
            </Button>
          )}
        </>
      )}

      {/*
        Docked at the bottom so both the rest clock and the finish sit under the thumb. It carries the
        countdown when there is one and the finish at all times — a member who is done after four of
        six exercises should not have to reach the end of a plan they are not going to complete.
      */}
      <div
        className="fixed inset-x-0 bottom-0 z-30 border-t border-border bg-background/95 backdrop-blur-md"
        style={{ paddingBottom: 'calc(env(safe-area-inset-bottom) + 5rem)' }}
      >
        <div className="mx-auto max-w-2xl space-y-3 p-4">
          {/*
            No printed denominator beside the countdown. REST_SECONDS is a flat 90s default, not a
            per-exercise prescription, and "1:12 of 2:00" would dress a constant up as a
            recommendation. The bar carries the same fraction without claiming to be advice.
            It slides in ABOVE the finish button rather than replacing it — see the block below.
          */}
          {resting && (
            <div className="animate-in fade-in slide-in-from-bottom-2 duration-[240ms] ease-[var(--ease-uplift)]">
              <div className="flex items-baseline justify-between">
                <span className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">Rest</span>
                <span className="font-display text-[30px] leading-none font-black tracking-tight text-primary tabular-nums [text-shadow:0_0_26px_rgb(214_249_74_/_0.35)]">
                  {clock(restRemaining)}
                </span>
              </div>
              <div className="mt-2 h-1.5 w-full overflow-hidden rounded-full bg-border">
                <div
                  className="h-full rounded-full transition-[width] duration-1000 ease-linear"
                  style={{
                    width: `${(restRemaining / REST_SECONDS) * 100}%`,
                    background: 'linear-gradient(90deg,#EDFF8A,#B8DC2B)',
                  }}
                />
              </div>
            </div>
          )}

          <div className="flex gap-2">
            {resting && (
              <Button variant="outline" className="press h-12 flex-1 rounded-2xl" onClick={skipRest}>
                Skip rest
              </Button>
            )}
            <Button
              className="press h-12 flex-1 rounded-2xl font-bold shadow-volt"
              disabled={logWorkout.isPending || entries.length === 0}
              onClick={finish}
            >
              {logWorkout.isPending && <Loader2 className="size-4 animate-spin" />}
              Finish · {entries.length} set{entries.length === 1 ? '' : 's'}
            </Button>
          </div>
        </div>
      </div>

      {celebration && (
        <WorkoutCelebration
          result={celebration}
          onDismiss={() => {
            setCelebration(null)
            navigate('/portal')
          }}
        />
      )}
    </div>
  )
}
