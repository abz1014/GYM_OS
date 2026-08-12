import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { ArrowUp, Check, Dumbbell, ListPlus, Loader2, Plus, Trash2, X } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'
import { MemberLoadError } from '@/modules/portal/components/portalShared'
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
  isSetMeasured,
  useActiveWorkout,
  type ActiveExercise,
  type ActiveSet,
} from '@/modules/portal/workout/activeWorkoutStore'
import { describeSets } from '@/lib/measurement'
import { ExercisePicker, toActiveExercise } from '@/modules/portal/workout/ExercisePicker'
import { isStale } from '@/shared/lib/queryTrust'

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

/** One numeric field in a set row, sized to be read at arm's length and tapped without looking. */
function SetField({
  id, label, value, step, placeholder, disabled, active, onChange,
}: {
  id: string
  label: string
  value: number | null
  step?: string
  placeholder?: string
  disabled: boolean
  active: boolean
  onChange: (value: number | null) => void
}) {
  return (
    <>
      <label className="sr-only" htmlFor={id}>{label}</label>
      <input
        id={id}
        type="number"
        inputMode={step ? 'decimal' : 'numeric'}
        step={step}
        min={0}
        disabled={disabled}
        value={value ?? ''}
        placeholder={placeholder ?? '—'}
        onChange={(e) => onChange(e.target.value === '' ? null : Number(e.target.value))}
        className={cn(
          // 21px/800 on an ink ground: this is read at arm's length, one-handed, mid-set. The active
          // row's fields take a dim olive border rather than a volt one — the row itself is already
          // outlined in volt, and a second volt line inside it competes with the first.
          'h-12 min-w-0 flex-1 rounded-xl border bg-background px-3 text-center font-display text-[21px] font-extrabold tabular-nums outline-none',
          active ? 'border-[#3A3E1F] text-foreground' : 'border-transparent',
          disabled ? 'bg-transparent text-muted-foreground' : 'text-foreground',
        )}
      />
    </>
  )
}

/**
 * The column headings for a movement, and the fields under them. One place, so they cannot disagree.
 *
 * A run is measured in distance and time; a plank in time; a press-up in reps; a bench press in reps
 * and kilograms. The row used to render "Kg" and "Reps" for every one of them, which is not a display
 * bug — it is where the fabricated data came from, because a field the member cannot leave blank gets
 * filled with something.
 */
const FIELDS_FOR: Record<string, { key: keyof ActiveSet; label: string; step?: string }[]> = {
  Weighted: [
    { key: 'weightKg', label: 'Kg', step: '0.5' },
    { key: 'reps', label: 'Reps' },
  ],
  Bodyweight: [{ key: 'reps', label: 'Reps' }],
  Timed: [{ key: 'durationSeconds', label: 'Seconds' }],
  Distance: [
    { key: 'distanceMeters', label: 'Metres' },
    { key: 'durationSeconds', label: 'Seconds' },
  ],
}

function fieldsFor(loadType: ActiveExercise['loadType']) {
  return FIELDS_FOR[loadType ?? 'Weighted'] ?? FIELDS_FOR.Weighted
}

/**
 * One row of the set table. The set being worked on is the only one that looks editable, because it
 * is the only one that is: finished sets are the record of what happened and are read back dimmed.
 */
function SetRow({
  index,
  set,
  loadType,
  active,
  onChange,
  onComplete,
}: {
  index: number
  set: ActiveSet
  loadType: ActiveExercise['loadType']
  active: boolean
  onChange: (patch: Partial<ActiveSet>) => void
  onComplete: () => void
}) {
  const done = set.done
  // The SAME predicate the send filter uses, so a set can never look logged and then be dropped.
  const measured = isSetMeasured(set, loadType)

  return (
    // Three states that must be told apart at a glance, mid-set, at arm's length. Done is dimmed
    // because it is history; upcoming keeps full-contrast text because it is still to do; only the
    // active row is outlined. Done and upcoming previously differed by a background one step apart on
    // a dark surface, which made a set the member had not started look like one they had finished.
    //
    // The active row carries three signals at once — a 1.5px volt border, a 4px halo and a coloured
    // drop shadow — because one of them alone is a border, and a border is what every other row
    // already has. The width is 1.5px on ALL three states so the stack never reflows by a half-pixel
    // as the active row moves down it.
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

      {fieldsFor(loadType).map((field) => (
        <SetField
          key={field.key}
          id={`set-${index}-${field.key}`}
          label={`Set ${index + 1} ${field.label}`}
          value={set[field.key] as number | null}
          step={field.step}
          disabled={done}
          active={active}
          onChange={(value) => onChange({ [field.key]: value } as Partial<ActiveSet>)}
        />
      ))}

      <button
        type="button"
        aria-label={done ? `Set ${index + 1} logged` : `Log set ${index + 1}`}
        /*
         * Cannot be confirmed without the measurement its movement is actually measured in.
         *
         * A member who cleared the field and tapped this saw a green, ticked, apparently-logged set
         * that the send filter then dropped — and the Finish button beside it counted the filtered
         * list, so the screen showed N ticks and a smaller number at the same time.
         */
        disabled={done || !active || !measured}
        onClick={onComplete}
        className={cn(
          // Filled at every stage, never an outline. This is the most-tapped control in the product
          // and it is tapped without looking; an empty circle asks the member to find an edge.
          'flex size-12 shrink-0 items-center justify-center rounded-xl transition-[transform,background-color,color] duration-[120ms] ease-[var(--ease-uplift)] active:scale-[.97]',
          done
            ? 'bg-success text-success-foreground'
            : active && measured
              ? 'bg-primary text-primary-foreground shadow-volt'
              : 'bg-muted',
        )}
      >
        {done && <Check className="size-5" />}
        {!done && active && measured && <Check className="size-5" />}
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

  const {
    startedAt, sourceLabel, exercises, currentIndex, restEndsAt,
    start, abandon, editSet, completeSet, addSet, addExercises, removeExercise, goToExercise, skipRest,
  } = useActiveWorkout()

  const running = startedAt !== null
  const now = useNow(running)
  const [pickerOpen, setPickerOpen] = useState(false)

  /**
   * The server's proposal, turned into a session the member could start — but NOT started.
   *
   * This used to run in an effect the moment the screen mounted, which made two claims that were
   * not true. It started the clock before the member had agreed to train, so a session's elapsed
   * time counted whatever they did between opening the app and picking up a weight. And it made the
   * proposal the only session available: the screen showed whatever was proposed and offered no way
   * to say "actually, today is back". Where the last log held one movement, that is a workout screen
   * with one exercise on it and nothing to add.
   *
   * Now the proposal is an offer beside a picker, and the clock starts on whichever one the member
   * taps. See ExercisePicker.
   */
  const proposed: ActiveExercise[] = (proposal.data?.entries ?? []).map((e) => ({
    exerciseId: e.exerciseId,
    exerciseName: e.exerciseName,
    // Without this the session rendered a proposed treadmill with KG and REPS columns — the default
    // for an unknown load type — and confirming sent a rep count the server rejects. A proposal the
    // member cannot confirm as proposed is a bug wearing a suggestion's clothes.
    loadType: e.loadType,
    // The proposal says "3 sets of 8 at 60kg"; the session turns that into three individual sets
    // the member confirms one at a time, each free to differ from the plan.
    sets: Array.from({ length: Math.max(1, e.sets) }, () => ({
      weightKg: e.weightKg,
      reps: e.reps,
      durationSeconds: e.durationSeconds,
      distanceMeters: e.distanceMeters,
      done: false,
    })),
  }))

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

  const picker = (
    <ExercisePicker
      open={pickerOpen}
      onClose={() => setPickerOpen(false)}
      alreadyInSession={exercises.map((e) => e.exerciseId)}
      onAdd={(picked) => addExercises('Your session', picked.map(toActiveExercise))}
    />
  )

  if (proposal.isLoading && !running) {
    return <Skeleton className="h-96 w-full rounded-3xl shimmer" />
  }

  /**
   * Before a session starts, this screen is a choice between two doors: take what the app already
   * knows you were going to do, or say what you are actually doing today.
   *
   * The proposal comes first because it is right most of the time — most sessions are a repeat with
   * a small change — but it is never the only option, and it is never presented as one. A member
   * whose plan is a single movement, or who came in for something else entirely, reaches the picker
   * in one tap rather than being handed a one-exercise workout and no way out of it.
   *
   * A failed proposal fetch is stated rather than absorbed. `!canConfirm` is satisfied equally by "no
   * history yet" and "the request didn't land", and telling someone standing at a rack that their
   * trainer hasn't set them anything — when in fact we just couldn't ask — is the one sentence here
   * they cannot check from where they are. The picker stays available underneath either way: it
   * reads a different endpoint, and the member is in the gym now.
   */
  if (!running) {
    return (
      <div className="mx-auto max-w-2xl space-y-4 pb-32">
        <div className="text-center">
          <Dumbbell className="mx-auto size-9 text-primary" />
          <h1 className="mt-3 font-display text-2xl font-black tracking-tight">Ready to train</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Pick what you're doing today — then just confirm each set.
          </p>
        </div>

        {isStale(proposal) && (
          <MemberLoadError
            title="We couldn't load your last session"
            hint="Your plan and your history are safe — we just can't reach them right now. You can still pick your exercises below."
            onRetry={() => void proposal.refetch()}
            isRetrying={proposal.isFetching}
          />
        )}

        {proposal.data?.canConfirm && proposed.length > 0 && (
          <div className="rounded-3xl border border-border bg-card p-4 edge-light">
            <p className="text-[11px] font-bold tracking-[0.12em] text-muted-foreground uppercase">
              {SESSION_SOURCE_LABEL[proposal.data.source] || 'Your session'}
            </p>
            <ul className="mt-3 space-y-1.5">
              {proposed.map((e) => (
                <li key={e.exerciseId} className="flex items-baseline justify-between gap-3">
                  <span className="min-w-0 truncate font-display font-bold tracking-tight">{e.exerciseName}</span>
                  <span className="shrink-0 text-sm text-muted-foreground tabular-nums">
                    {describeSets({
                      sets: e.sets.length,
                      reps: e.sets[0]?.reps,
                      weightKg: e.sets[0]?.weightKg,
                      durationSeconds: e.sets[0]?.durationSeconds,
                      distanceMeters: e.sets[0]?.distanceMeters,
                    })}
                  </span>
                </li>
              ))}
            </ul>
            <Button
              className="press mt-4 h-12 w-full rounded-2xl font-bold shadow-volt"
              onClick={() => start(SESSION_SOURCE_LABEL[proposal.data!.source] || 'Your session', proposed)}
            >
              Start this session
            </Button>
          </div>
        )}

        <Button
          variant={proposal.data?.canConfirm ? 'outline' : 'default'}
          className={cn(
            'press h-12 w-full rounded-2xl font-bold',
            !proposal.data?.canConfirm && 'shadow-volt',
          )}
          onClick={() => setPickerOpen(true)}
        >
          <ListPlus className="size-4" />
          {proposal.data?.canConfirm ? 'Pick different exercises' : 'Pick your exercises'}
        </Button>

        <Button asChild variant="ghost" className="press h-11 w-full rounded-2xl text-muted-foreground">
          <Link to="/log-activity">Log a past workout instead</Link>
        </Button>

        {picker}
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
            {/* Headings come from the same table the fields do, so they can never label the wrong
                box — a run reads "Metres | Seconds", a plank reads "Seconds". */}
            <span className="w-7 shrink-0 text-center">Set</span>
            {fieldsFor(exercise.loadType).map((f) => (
              <span key={f.key} className="flex-1 text-center">{f.label}</span>
            ))}
            <span className="size-12 shrink-0" />
          </div>

          <div className="mt-1.5 space-y-2">
            {exercise.sets.map((s, i) => (
              <SetRow
                key={i}
                index={i}
                set={s}
                loadType={exercise.loadType}
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

          {/* Dropping a movement is only offered while it is still only a plan. Once a set on it has
              been logged it is a record of something that happened, and removing it here would
              silently delete work the member did. */}
          {exercise.sets.every((s) => !s.done) && exercises.length > 1 && (
            <button
              type="button"
              onClick={() => removeExercise(currentIndex)}
              className="press mt-3 flex h-11 w-full items-center justify-center gap-2 rounded-2xl text-sm font-medium text-muted-foreground hover:text-destructive"
            >
              <Trash2 className="size-4" />
              Remove {exercise.exerciseName}
            </button>
          )}
        </>
      )}

      {/*
        A session is not a fixed list. Members add a movement halfway through constantly — a machine
        was taken, they felt good and did one more, a friend suggested something — and before this
        the only way to record it was to finish, then log a second session that would count as two.
      */}
      <button
        type="button"
        onClick={() => setPickerOpen(true)}
        className="press mt-4 flex h-12 w-full items-center justify-center gap-2 rounded-2xl border border-dashed border-border text-sm font-medium text-muted-foreground hover:text-foreground"
      >
        <ListPlus className="size-4" />
        Add exercise
      </button>

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

      {picker}

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
