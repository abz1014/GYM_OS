import { useMemo, useState } from 'react'
import { Check, Search, X } from 'lucide-react'

import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'
import { MemberLoadError } from '@/modules/portal/components/portalShared'
import { useMyExerciseCatalogue, type CatalogueExercise } from '@/modules/portal/api/portalApi'
import { isStale } from '@/shared/lib/queryTrust'

/** Sets a movement opens with when the member has never done it. Three is the default everywhere. */
const DEFAULT_SETS = 3
const DEFAULT_REPS = 8

/**
 * The virtual "Recent" category. Not a muscle group and deliberately first: in a gym the single
 * best predictor of what someone is about to do is what they have done before, and a member on a
 * four-day split reaches this list far more often than they browse Chest.
 */
const RECENT_KEY = '__recent'
const RECENT_LIMIT = 12

function daysAgoLabel(iso: string | null): string | null {
  if (!iso) return null
  const days = Math.floor((Date.now() - new Date(iso).getTime()) / 86_400_000)
  if (days <= 0) return 'today'
  if (days === 1) return 'yesterday'
  if (days < 7) return `${days}d ago`
  if (days < 30) return `${Math.floor(days / 7)}w ago`
  return `${Math.floor(days / 30)}mo ago`
}

/**
 * What the member last did on a movement, said only when it is true.
 *
 * Weighted movements read "3 × 8 · 60kg"; a plank reads "3 × 8" with no load, because it has none.
 * A movement they have never done says nothing at all rather than showing a zero or a dash that
 * looks like a measurement.
 */
function lastLine(exercise: CatalogueExercise): string | null {
  const when = daysAgoLabel(exercise.lastPerformedAt)
  if (!when || exercise.lastSets === null || exercise.lastReps === null) return null

  const load = exercise.lastWeightKg !== null ? ` · ${exercise.lastWeightKg}kg` : ''
  return `${exercise.lastSets} × ${exercise.lastReps}${load} · ${when}`
}

/** What a picked movement becomes in the live session, pre-filled from the member's own history. */
export function toActiveExercise(exercise: CatalogueExercise) {
  const sets = exercise.lastSets ?? DEFAULT_SETS
  const reps = exercise.lastReps ?? DEFAULT_REPS

  return {
    exerciseId: exercise.exerciseId,
    exerciseName: exercise.name,
    sets: Array.from({ length: Math.max(1, sets) }, () => ({
      weightKg: exercise.lastWeightKg,
      reps,
      done: false,
    })),
  }
}

function ExerciseRow({
  exercise,
  selected,
  onToggle,
}: {
  exercise: CatalogueExercise
  selected: boolean
  onToggle: () => void
}) {
  const last = lastLine(exercise)

  return (
    <button
      type="button"
      onClick={onToggle}
      aria-pressed={selected}
      className={cn(
        'press flex w-full items-center gap-3 rounded-2xl border-[1.5px] p-3 text-left transition-colors duration-[160ms]',
        selected ? 'border-primary bg-accent' : 'border-border hover:border-muted-foreground/40',
      )}
    >
      <span
        className={cn(
          'flex size-8 shrink-0 items-center justify-center rounded-lg transition-colors',
          selected ? 'bg-primary text-primary-foreground' : 'bg-muted text-transparent',
        )}
      >
        <Check className="size-4" />
      </span>

      <span className="min-w-0 flex-1">
        <span className="block truncate font-display font-bold tracking-tight">{exercise.name}</span>
        <span className="block truncate text-xs text-muted-foreground">
          {last ?? exercise.equipment ?? exercise.muscleGroupName}
        </span>
      </span>

      {/* Equipment is worth a second glance only when the line above is already spoken for. */}
      {last && exercise.equipment && (
        <span className="hidden shrink-0 rounded-lg bg-muted px-2 py-1 text-[11px] font-medium text-muted-foreground sm:block">
          {exercise.equipment}
        </span>
      )}
    </button>
  )
}

/**
 * Choose what you are training today. The one screen between opening the app in the gym and logging.
 *
 * The product rule this exists to serve: the member should only ever type SETS and WEIGHT. Which
 * movement, which muscle group, how many sets they did last time and what was on the bar are all
 * things the app already knows, so this asks them to pick from what their gym actually has rather
 * than to compose a session from memory.
 *
 * Three affordances, in the order they get used:
 *
 * - Search, because a member who knows they want incline dumbbell press should not browse for it.
 * - Recent, because most sessions are variations of previous ones.
 * - Muscle-group tabs, because a member deciding in the moment thinks "chest today", not "bench".
 *
 * Selection is multiple and the sheet stays open across it: picking five movements is one visit,
 * not five. Nothing is written anywhere until "Add" — closing this changes nothing.
 */
export function ExercisePicker({
  open,
  onClose,
  onAdd,
  alreadyInSession,
}: {
  open: boolean
  onClose: () => void
  onAdd: (exercises: CatalogueExercise[]) => void
  /** Exercise ids already in the running session — shown as picked and impossible to add twice. */
  alreadyInSession: string[]
}) {
  const catalogue = useMyExerciseCatalogue()
  const [search, setSearch] = useState('')
  const [activeGroup, setActiveGroup] = useState<string>(RECENT_KEY)
  const [picked, setPicked] = useState<Record<string, CatalogueExercise>>({})

  const inSession = useMemo(() => new Set(alreadyInSession), [alreadyInSession])
  const groups = catalogue.data?.groups ?? []

  const allExercises = useMemo(() => groups.flatMap((g) => g.exercises), [groups])

  const recent = useMemo(
    () =>
      allExercises
        .filter((e) => e.lastPerformedAt !== null)
        .sort((a, b) => (b.lastPerformedAt ?? '').localeCompare(a.lastPerformedAt ?? ''))
        .slice(0, RECENT_LIMIT),
    [allExercises],
  )

  /*
   * Search spans the whole catalogue and ignores the selected tab. Someone typing "row" wants every
   * row their gym has, not the rows filed under whichever group happened to be open — narrowing a
   * search by a tab the member is not looking at is how a catalogue starts feeling incomplete.
   */
  const visible = useMemo(() => {
    const term = search.trim().toLowerCase()
    if (term) {
      return allExercises.filter(
        (e) =>
          e.name.toLowerCase().includes(term) ||
          (e.equipment ?? '').toLowerCase().includes(term) ||
          e.muscleGroupName.toLowerCase().includes(term),
      )
    }
    if (activeGroup === RECENT_KEY) return recent
    return groups.find((g) => g.key === activeGroup)?.exercises ?? []
  }, [search, activeGroup, allExercises, recent, groups])

  const pickedList = Object.values(picked)

  if (!open) return null

  const toggle = (exercise: CatalogueExercise) => {
    if (inSession.has(exercise.exerciseId)) return
    setPicked((current) => {
      const next = { ...current }
      if (next[exercise.exerciseId]) delete next[exercise.exerciseId]
      else next[exercise.exerciseId] = exercise
      return next
    })
  }

  const confirm = () => {
    onAdd(pickedList)
    setPicked({})
    setSearch('')
    onClose()
  }

  const dismiss = () => {
    setPicked({})
    setSearch('')
    onClose()
  }

  // Recent only earns its tab once there is something in it — an empty first tab teaches the member
  // the picker is broken before they have used it once.
  const tabs = [
    ...(recent.length > 0 ? [{ key: RECENT_KEY, name: 'Recent' }] : []),
    ...groups.filter((g) => g.exercises.length > 0).map((g) => ({ key: g.key, name: g.name })),
  ]
  const effectiveGroup = tabs.some((t) => t.key === activeGroup) ? activeGroup : tabs[0]?.key

  return (
    <div className="fixed inset-0 z-50 flex flex-col bg-background">
      <div className="flex items-center gap-3 border-b border-border p-4">
        <button
          type="button"
          onClick={dismiss}
          aria-label="Close exercise picker"
          className="press flex size-11 shrink-0 items-center justify-center rounded-2xl bg-muted text-muted-foreground edge-light hover:text-foreground"
        >
          <X className="size-5" />
        </button>
        <div className="min-w-0 flex-1">
          <h2 className="font-display text-lg font-black tracking-tight">What are you training?</h2>
          <p className="text-xs text-muted-foreground">Pick your movements — sets and weight come next.</p>
        </div>
      </div>

      <div className="border-b border-border px-4 pb-3">
        <div className="relative mt-3">
          <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground" />
          <input
            type="search"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Search exercises"
            aria-label="Search exercises"
            className="h-12 w-full rounded-2xl border border-border bg-muted pr-3 pl-10 text-sm outline-none focus:border-primary"
          />
        </div>

        {/* Hidden while searching: the results already span every group, so a highlighted tab would
            claim a filter that is not being applied. */}
        {!search.trim() && tabs.length > 0 && (
          <div className="-mx-4 mt-3 overflow-x-auto px-4">
            <div className="flex gap-2">
              {tabs.map((tab) => (
                <button
                  key={tab.key}
                  type="button"
                  onClick={() => setActiveGroup(tab.key)}
                  className={cn(
                    'press shrink-0 rounded-xl px-3 py-2 text-sm font-bold whitespace-nowrap transition-colors',
                    tab.key === effectiveGroup
                      ? 'bg-primary text-primary-foreground shadow-volt'
                      : 'bg-muted text-muted-foreground hover:text-foreground',
                  )}
                >
                  {tab.name}
                </button>
              ))}
            </div>
          </div>
        )}
      </div>

      <div className="min-h-0 flex-1 overflow-y-auto p-4">
        {catalogue.isLoading && (
          <div className="space-y-2">
            {Array.from({ length: 6 }, (_, i) => (
              <Skeleton key={i} className="h-16 w-full rounded-2xl shimmer" />
            ))}
          </div>
        )}

        {/* The catalogue IS this screen. An empty list from a failed fetch would tell a member
            standing in their gym that it has no equipment. */}
        {isStale(catalogue) && (
          <MemberLoadError
            title="We couldn't load the exercise list"
            hint="Your gym's exercises are still there — we just can't reach them right now."
            onRetry={() => void catalogue.refetch()}
            isRetrying={catalogue.isFetching}
          />
        )}

        {!catalogue.isLoading && !isStale(catalogue) && visible.length === 0 && (
          <p className="pt-8 text-center text-sm text-muted-foreground">
            {search.trim() ? `Nothing matching "${search.trim()}".` : 'Nothing in this group yet.'}
          </p>
        )}

        <div className="space-y-2">
          {visible.map((exercise) => (
            <ExerciseRow
              key={exercise.exerciseId}
              exercise={exercise}
              selected={inSession.has(exercise.exerciseId) || Boolean(picked[exercise.exerciseId])}
              onToggle={() => toggle(exercise)}
            />
          ))}
        </div>
      </div>

      <div
        className="border-t border-border bg-background/95 p-4 backdrop-blur-md"
        style={{ paddingBottom: 'calc(env(safe-area-inset-bottom) + 1rem)' }}
      >
        <Button
          className="press h-12 w-full rounded-2xl font-bold shadow-volt"
          disabled={pickedList.length === 0}
          onClick={confirm}
        >
          {pickedList.length === 0
            ? 'Pick at least one'
            : `Add ${pickedList.length} exercise${pickedList.length === 1 ? '' : 's'}`}
        </Button>
      </div>
    </div>
  )
}
