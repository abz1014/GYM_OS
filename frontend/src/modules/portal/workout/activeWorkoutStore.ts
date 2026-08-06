import { create } from 'zustand'
import { persist } from 'zustand/middleware'

/** One set as the member is filling it in. `done` is what separates a plan from a record. */
export interface ActiveSet {
  weightKg: number | null
  reps: number
  done: boolean
}

export interface ActiveExercise {
  exerciseId: string
  exerciseName: string
  sets: ActiveSet[]
}

/** Fixed default rest between sets. Nothing in the product prescribes one per exercise, so this is a
 *  plain constant rather than a number dressed up as a recommendation. The member can skip it. */
export const REST_SECONDS = 90

interface ActiveWorkoutState {
  /** Epoch ms the session began, or null when nothing is running. */
  startedAt: number | null
  /** What the session was proposed as ("Today's plan", "Same as last time"), for the header. */
  sourceLabel: string
  exercises: ActiveExercise[]
  currentIndex: number
  /** Epoch ms the rest countdown ends, or null when not resting. */
  restEndsAt: number | null

  start: (sourceLabel: string, exercises: ActiveExercise[]) => void
  abandon: () => void
  editSet: (exerciseIndex: number, setIndex: number, patch: Partial<ActiveSet>) => void
  completeSet: (exerciseIndex: number, setIndex: number) => void
  addSet: (exerciseIndex: number) => void
  goToExercise: (index: number) => void
  skipRest: () => void
}

/**
 * The live session — the one piece of member state the server does not hold while it is happening.
 *
 * Two things make this a store rather than component state. It has to survive navigation: a member
 * mid-session who taps Home and comes back must find their session and their rest timer exactly where
 * they left them, and unmounting the page would otherwise take both with it. And it has to survive a
 * reload, because a phone locking or a browser reclaiming a backgrounded tab is not the member
 * abandoning their workout — hence `persist`.
 *
 * Time is stored as ABSOLUTE epoch milliseconds (`startedAt`, `restEndsAt`), never as a counter this
 * ticks down. A stored countdown is wrong the instant the tab stops rendering — backgrounded, locked,
 * or simply not re-rendering — because it only decrements when something is running. Derived from two
 * timestamps, elapsed and remaining are correct on the first frame after the member comes back, which
 * is the whole reason a rest timer is trustworthy.
 *
 * Nothing here is written to the server. A session becomes real only when the member finishes it and
 * the completed sets are posted through the same /api/me/workouts path a one-tap confirmation uses —
 * one place where a session becomes a record, one set of rules, one cascade.
 */
export const useActiveWorkout = create<ActiveWorkoutState>()(
  persist(
    (set) => ({
      startedAt: null,
      sourceLabel: '',
      exercises: [],
      currentIndex: 0,
      restEndsAt: null,

      start: (sourceLabel, exercises) =>
        set({ startedAt: Date.now(), sourceLabel, exercises, currentIndex: 0, restEndsAt: null }),

      abandon: () => set({ startedAt: null, sourceLabel: '', exercises: [], currentIndex: 0, restEndsAt: null }),

      editSet: (exerciseIndex, setIndex, patch) =>
        set((s) => ({
          exercises: s.exercises.map((ex, i) =>
            i !== exerciseIndex
              ? ex
              : { ...ex, sets: ex.sets.map((st, j) => (j === setIndex ? { ...st, ...patch } : st)) },
          ),
        })),

      completeSet: (exerciseIndex, setIndex) =>
        set((s) => ({
          exercises: s.exercises.map((ex, i) =>
            i !== exerciseIndex
              ? ex
              : { ...ex, sets: ex.sets.map((st, j) => (j === setIndex ? { ...st, done: true } : st)) },
          ),
          // Resting starts the moment a set is finished — that is when the clock actually starts, not
          // when the member gets round to looking at the screen.
          restEndsAt: Date.now() + REST_SECONDS * 1000,
        })),

      addSet: (exerciseIndex) =>
        set((s) => ({
          exercises: s.exercises.map((ex, i) => {
            if (i !== exerciseIndex) return ex
            // A new set starts from the last one: the member is far more often repeating a load than
            // choosing a fresh one, and an empty row asks them to retype what is already on the bar.
            const previous = ex.sets.at(-1)
            return {
              ...ex,
              sets: [...ex.sets, { weightKg: previous?.weightKg ?? null, reps: previous?.reps ?? 8, done: false }],
            }
          }),
        })),

      goToExercise: (index) => set({ currentIndex: index, restEndsAt: null }),

      skipRest: () => set({ restEndsAt: null }),
    }),
    { name: 'gymos-active-workout' },
  ),
)

/** The set the member is on: the first unfinished one, or null when the exercise is done. */
export function firstUnfinishedSet(exercise: ActiveExercise | undefined): number | null {
  if (!exercise) return null
  const index = exercise.sets.findIndex((s) => !s.done)
  return index === -1 ? null : index
}

/** Every completed set across the session, flattened into what /api/me/workouts accepts.
 *
 *  One entry PER SET rather than one per exercise: the write path takes a list of (exercise, sets,
 *  reps, weight) rows and the projections behind it reduce all rows sharing an exercise together
 *  (PersonalRecordPolicy.StatsFor takes the list; mastery counts DISTINCT workout logs, not entries),
 *  so per-set rows record what actually happened — 85kg then 85kg then 87.5kg — instead of flattening
 *  three different loads into one average nobody lifted. */
export function completedEntries(exercises: ActiveExercise[]) {
  return exercises.flatMap((ex) =>
    ex.sets
      .filter((s) => s.done && s.reps > 0)
      .map((s) => ({
        exerciseId: ex.exerciseId,
        setsCompleted: 1,
        repsCompleted: s.reps,
        weightKg: s.weightKg,
      })),
  )
}
