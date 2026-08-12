import { useMemo, useState } from 'react'
import { Check, Dumbbell, Loader2, Minus, Plus, RotateCcw, Search, X } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import { MemberLoadError } from '@/modules/portal/components/portalShared'
import { describeSets } from '@/lib/measurement'
import { WorkoutCelebration } from '@/modules/portal/components/WorkoutCelebration'
import {
  SESSION_SOURCE_LABEL,
  useLogMyWorkout,
  useMyLoggingOptions,
  useMyNextSession,
  useMyWorkoutSuggestions,
  type MyWorkoutResult,
  type WorkoutEntryInput,
  type ExerciseLoadType,
} from '@/modules/portal/api/portalApi'
import { isStale } from '@/shared/lib/queryTrust'

/**
 * Logging built for someone standing at a rack holding a phone, not sitting at a spreadsheet.
 *
 * The design rule here is "tap, don't type": the previous version asked for a dropdown plus three
 * typed numbers per exercise, which nobody finishes mid-session. Instead the member's own history
 * supplies the defaults — their usual lifts are the buttons, and each one arrives pre-filled with
 * exactly what they did last time — so the common case ("same as last time") is one tap, and
 * changing the weight is a thumb-sized +/- rather than a keyboard.
 */

const WEIGHT_STEP = 2.5 // the smallest real plate pair on most racks

interface PendingEntry {
  key: number
  exerciseId: string
  exerciseName: string
  loadType: ExerciseLoadType
  sets: number
  reps: number | null
  weight: number | null
  seconds: number | null
  metres: number | null
}

let entryKey = 0

function Stepper({
  label,
  value,
  onChange,
  step = 1,
  min = 0,
  suffix = '',
}: {
  label: string
  value: number
  onChange: (next: number) => void
  step?: number
  min?: number
  suffix?: string
}) {
  return (
    <div className="flex items-center justify-between gap-3">
      <span className="text-sm text-muted-foreground">{label}</span>
      <div className="flex items-center gap-1">
        <Button
          type="button"
          variant="outline"
          size="icon"
          className="size-11 shrink-0 rounded-full"
          aria-label={`Decrease ${label}`}
          onClick={() => onChange(Math.max(min, Number((value - step).toFixed(2))))}
        >
          <Minus className="size-5" />
        </Button>
        <span className="min-w-[5.5rem] text-center text-xl font-semibold tabular-nums">
          {value}
          {suffix && <span className="ml-0.5 text-sm font-normal text-muted-foreground">{suffix}</span>}
        </span>
        <Button
          type="button"
          variant="outline"
          size="icon"
          className="size-11 shrink-0 rounded-full"
          aria-label={`Increase ${label}`}
          onClick={() => onChange(Number((value + step).toFixed(2)))}
        >
          <Plus className="size-5" />
        </Button>
      </div>
    </div>
  )
}

export function QuickLogWorkout() {
  const suggestions = useMyWorkoutSuggestions()
  const options = useMyLoggingOptions()
  const proposal = useMyNextSession()
  const logWorkout = useLogMyWorkout()

  const [pending, setPending] = useState<PendingEntry[]>([])
  // Holds the just-logged session's result while the celebration is on screen; null means dismissed.
  const [celebration, setCelebration] = useState<MyWorkoutResult | null>(null)
  const [openExerciseId, setOpenExerciseId] = useState<string | null>(null)
  const [draft, setDraft] = useState({ sets: 3, reps: 10, weight: 0, seconds: 0, metres: 0 })
  const [showAll, setShowAll] = useState(false)
  const [search, setSearch] = useState('')

  // The member's own lifts, most recent first — these become the primary buttons.
  const usualLifts = useMemo(() => (suggestions.data ?? []).slice(0, 6), [suggestions.data])

  // The same session the home screen offers, from the same server-side policy — this screen loads it
  // into the editor instead of committing it. Reading the raw most-recent log here instead made the
  // two screens disagree: it ignored an active trainer plan, and a log with no entries (a check-in
  // with nothing recorded against it) hid this shortcut entirely while home still offered a session.
  const proposed = proposal.data?.canConfirm ? proposal.data.entries : []

  /**
   * What the open movement is measured in.
   *
   * This screen was the SECOND independent source of the fabricated rep count — it defaulted to 10
   * reps for everything, with no reference to load type, exactly as the picker defaulted to 8. The
   * server now rejects a rep count on a run, so without this the form would simply 400.
   *
   * "Usual lifts" come from the overload suggestions, which are Weighted-only by construction, so
   * anything reached that way is a lift; only the browse list can surface a run or a plank.
   */
  const openLoadType = useMemo(() => {
    if (!openExerciseId) return 'Weighted'
    const fromCatalogue = options.data?.exercises.find((e) => e.id === openExerciseId)
    return (fromCatalogue?.loadType ?? 'Weighted') as ExerciseLoadType
  }, [openExerciseId, options.data?.exercises])

  const hasReps = openLoadType === 'Weighted' || openLoadType === 'Bodyweight'

  const otherExercises = useMemo(() => {
    const usualIds = new Set(usualLifts.map((s) => s.exerciseId))
    const all = options.data?.exercises ?? []
    const term = search.trim().toLowerCase()
    return all
      .filter((e) => !usualIds.has(e.id))
      .filter((e) => (term ? e.name.toLowerCase().includes(term) : true))
  }, [options.data?.exercises, usualLifts, search])

  const openFor = (exerciseId: string, exerciseName: string, weight: number | null, reps: number | null) => {
    if (openExerciseId === exerciseId) {
      setOpenExerciseId(null)
      return
    }
    setOpenExerciseId(exerciseId)
    // Default to what they actually did last time; reps come back as a session total, so divide
    // down to a per-set figure rather than showing an implausible "40 reps".
    const sets = 3
    setDraft({
      sets,
      reps: reps ? Math.max(1, Math.round(reps / sets)) : 10,
      weight: weight ?? 0,
      // Nothing is pre-filled for time or distance: unlike a load there is no "what you did last
      // time" carried on the suggestion, and a default would be the same invention in a new field.
      seconds: 0,
      metres: 0,
    })
    setSearch('')
    setShowAll(false)
    void exerciseName
  }

  const addPending = (exerciseId: string, exerciseName: string) => {
    setPending((p) => [
      ...p,
      {
        key: entryKey++,
        exerciseId,
        exerciseName,
        loadType: openLoadType,
        sets: hasReps ? draft.sets : 1,
        reps: hasReps ? draft.reps : null,
        weight: openLoadType === 'Bodyweight' || openLoadType === 'Timed' ? null : draft.weight || null,
        seconds: hasReps ? null : draft.seconds || null,
        metres: openLoadType === 'Distance' ? draft.metres || null : null,
      },
    ])
    setOpenExerciseId(null)
  }

  const loadProposedSession = () => {
    if (!proposed.length) return
    setPending(
      proposed.map((e) => ({
        key: entryKey++,
        exerciseId: e.exerciseId,
        exerciseName: e.exerciseName,
        // The proposal knows what each movement is measured in — hardcoding Weighted here loaded a
        // run as a lift and dropped the distance it carried.
        loadType: e.loadType,
        sets: e.sets,
        reps: e.reps,
        weight: e.weightKg,
        seconds: e.durationSeconds,
        metres: e.distanceMeters,
      })),
    )
    toast.success('Loaded — tweak anything, then finish.')
  }

  const finish = () => {
    const entries: WorkoutEntryInput[] = pending.map((p) => ({
      exerciseId: p.exerciseId,
      setsCompleted: p.sets,
      repsCompleted: p.reps,
      weightKg: p.weight,
      durationSeconds: p.seconds,
      distanceMeters: p.metres,
    }))
    logWorkout.mutate(entries, {
      // The result replaces the old toast entirely. That toast claimed a flat "+50 XP" whatever the
      // engine actually awarded, and vanished in three seconds — a poor answer to the only effort
      // the app asks a member to make.
      onSuccess: (result) => {
        setCelebration(result)
        setPending([])
      },
      onError: () => toast.error("Couldn't save that workout."),
    })
  }

  if (suggestions.isLoading || options.isLoading) return <Skeleton className="h-72 w-full" />

  // Everything the member can pick from comes from one of three queries, and they fail separately.
  // Losing `suggestions` costs the usual-lifts row, losing `proposal` costs the load-the-session
  // shortcut; both are shortcuts, and a shortcut that quietly isn't there is survivable. Losing all
  // three leaves a screen with a search box over an empty catalogue and no way to log anything, which
  // is worth saying out loud rather than presenting as a gym with no exercises in it.
  if (isStale(options) && usualLifts.length === 0 && proposed.length === 0) {
    return (
      <MemberLoadError
        title="We couldn't load the exercise list"
        hint="Nothing you've logged before is lost — we just can't reach the gym right now."
        onRetry={() => void options.refetch()}
        isRetrying={options.isFetching}
      />
    )
  }

  return (
    <div className="space-y-4">
      {/* One tap to load the whole session — the fastest path for anyone on a fixed program. */}
      {proposed.length ? (
        <button
          type="button"
          onClick={loadProposedSession}
          className="flex w-full items-center gap-3 rounded-xl border-2 border-dashed p-4 text-left transition-colors hover:border-primary hover:bg-accent"
        >
          <RotateCcw className="size-5 shrink-0 text-primary" />
          <div className="min-w-0">
            <p className="font-medium">{SESSION_SOURCE_LABEL[proposal.data!.source]}</p>
            <p className="truncate text-sm text-muted-foreground">
              {/* describeSets, not `${sets}×${reps}` — a run has no reps and the inline version
                  rendered the dangling "1×" this codebase keeps re-learning to avoid. */}
              {proposed
                .map(
                  (e) =>
                    `${e.exerciseName} ${describeSets({
                      sets: e.sets,
                      reps: e.reps,
                      weightKg: e.weightKg,
                      durationSeconds: e.durationSeconds,
                      distanceMeters: e.distanceMeters,
                    })}`,
                )
                .join(' · ')}
            </p>
          </div>
        </button>
      ) : null}

      <div>
        <p className="mb-2 text-sm font-medium">What did you do today?</p>
        <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
          {usualLifts.map((s) => (
            <div key={s.exerciseId} className="contents">
              <button
                type="button"
                onClick={() => openFor(s.exerciseId, s.exerciseName, s.lastWeightKg, s.lastTotalReps)}
                className={`flex min-h-16 flex-col justify-center rounded-xl border-2 p-3 text-left transition-colors ${
                  openExerciseId === s.exerciseId ? 'border-primary bg-accent' : 'hover:border-primary/50 hover:bg-accent'
                }`}
              >
                <span className="font-medium leading-tight">{s.exerciseName}</span>
                <span className="text-xs text-muted-foreground">
                  {s.lastWeightKg ? `last ${s.lastWeightKg} kg` : 'bodyweight'}
                </span>
              </button>
            </div>
          ))}

          <button
            type="button"
            onClick={() => {
              setShowAll((v) => !v)
              setOpenExerciseId(null)
            }}
            className="flex min-h-16 flex-col items-center justify-center rounded-xl border-2 border-dashed p-3 text-muted-foreground transition-colors hover:border-primary/50 hover:bg-accent"
          >
            <Plus className="size-5" />
            <span className="text-xs">Something else</span>
          </button>
        </div>
      </div>

      {/* The stepper panel: pre-filled, thumb-sized, no keyboard. */}
      {openExerciseId && (
        <Card className="border-primary">
          <CardContent className="space-y-4 p-4">
            <p className="font-medium">
              {usualLifts.find((s) => s.exerciseId === openExerciseId)?.exerciseName ??
                options.data?.exercises.find((e) => e.id === openExerciseId)?.name}
            </p>
            {/* Only the measurements this movement has. A field a member cannot leave blank gets
                filled with something, which is precisely how the fabricated numbers got in. */}
            {openLoadType !== 'Bodyweight' && openLoadType !== 'Timed' && (
              <Stepper label="Weight" value={draft.weight} step={WEIGHT_STEP} suffix="kg" onChange={(weight) => setDraft((d) => ({ ...d, weight }))} />
            )}
            {hasReps && (
              <>
                <Stepper label="Sets" value={draft.sets} min={1} onChange={(sets) => setDraft((d) => ({ ...d, sets }))} />
                <Stepper label="Reps" value={draft.reps} min={1} onChange={(reps) => setDraft((d) => ({ ...d, reps }))} />
              </>
            )}
            {openLoadType === 'Distance' && (
              <Stepper label="Distance" value={draft.metres} step={100} suffix="m" onChange={(metres) => setDraft((d) => ({ ...d, metres }))} />
            )}
            {!hasReps && (
              <Stepper label="Time" value={draft.seconds} step={30} suffix="s" onChange={(seconds) => setDraft((d) => ({ ...d, seconds }))} />
            )}
            <Button
              className="h-12 w-full text-base"
              onClick={() =>
                addPending(
                  openExerciseId,
                  usualLifts.find((s) => s.exerciseId === openExerciseId)?.exerciseName ??
                    options.data?.exercises.find((e) => e.id === openExerciseId)?.name ??
                    'Exercise',
                )
              }
            >
              <Check className="size-5" />
              Add to workout
            </Button>
          </CardContent>
        </Card>
      )}

      {showAll && (
        <Card>
          <CardContent className="space-y-3 p-4">
            <div className="relative">
              <Search className="absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                autoFocus
                placeholder="Search exercises…"
                className="pl-9"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
              />
            </div>
            {/*
              `otherExercises` is filtered out of `options.data?.exercises ?? []`, so a catalogue that
              failed to arrive matches nothing — and the panel answered every term the member typed
              with "No matches.", which is a claim that this gym has no exercise by that name. It said
              it for "bench", confidently, once per keystroke.
              This stays a panel-level error rather than taking the screen down with it: the row of
              usual lifts above comes from a different query and is frequently still working, and
              logging what you always log is the path most members are on anyway.
            */}
            {isStale(options) ? (
              <MemberLoadError
                title="We couldn't load the exercise list"
                hint="This is our connection, not your gym's catalogue — your usual lifts above still work."
                onRetry={() => void options.refetch()}
                isRetrying={options.isFetching}
              />
            ) : (
              <div className="grid max-h-64 grid-cols-2 gap-2 overflow-y-auto sm:grid-cols-3">
                {otherExercises.map((e) => (
                  <button
                    key={e.id}
                    type="button"
                    onClick={() => openFor(e.id, e.name, null, null)}
                    className="min-h-14 rounded-xl border-2 p-2 text-left text-sm transition-colors hover:border-primary/50 hover:bg-accent"
                  >
                    <span className="font-medium leading-tight">{e.name}</span>
                    {e.muscleGroup && <span className="block text-xs text-muted-foreground">{e.muscleGroup}</span>}
                  </button>
                ))}
                {otherExercises.length === 0 && (
                  <p className="col-span-full py-4 text-center text-sm text-muted-foreground">No matches.</p>
                )}
              </div>
            )}
          </CardContent>
        </Card>
      )}

      {pending.length > 0 && (
        <Card>
          <CardContent className="space-y-2 p-4">
            <p className="text-sm font-medium">Today's workout</p>
            {pending.map((p) => (
              <div key={p.key} className="flex items-center justify-between gap-2 rounded-lg bg-muted/50 px-3 py-2">
                <span className="min-w-0 truncate text-sm">
                  <span className="font-medium">{p.exerciseName}</span>{' '}
                  <span className="text-muted-foreground">
                    {describeSets({
                      sets: p.sets,
                      reps: p.reps,
                      weightKg: p.weight,
                      durationSeconds: p.seconds,
                      distanceMeters: p.metres,
                    })}
                  </span>
                </span>
                <Button
                  variant="ghost"
                  size="icon"
                  className="size-8 shrink-0"
                  aria-label={`Remove ${p.exerciseName}`}
                  onClick={() => setPending((all) => all.filter((x) => x.key !== p.key))}
                >
                  <X className="size-4" />
                </Button>
              </div>
            ))}
          </CardContent>
        </Card>
      )}

      <Button
        className="h-14 w-full text-base"
        disabled={pending.length === 0 || logWorkout.isPending}
        onClick={finish}
      >
        {logWorkout.isPending ? <Loader2 className="size-5 animate-spin" /> : <Dumbbell className="size-5" />}
        {pending.length === 0
          ? 'Pick an exercise to start'
          : `Finish workout · ${pending.length} exercise${pending.length === 1 ? '' : 's'}`}
      </Button>

      {celebration && <WorkoutCelebration result={celebration} onDismiss={() => setCelebration(null)} />}
    </div>
  )
}
