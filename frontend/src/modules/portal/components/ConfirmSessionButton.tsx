import { Link } from 'react-router-dom'
import { ChevronRight, Dumbbell, Loader2 } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import {
  SESSION_SOURCE_LABEL,
  useLogMyWorkout,
  useMyNextSession,
  type MyWorkoutResult,
  type ProposedEntry,
} from '@/modules/portal/api/portalApi'

function summarise(entries: ProposedEntry[]): string {
  const first = entries[0]
  if (!first) return ''
  const load = first.weightKg === null ? '' : ` · ${first.weightKg}kg`
  const rest = entries.length > 1 ? ` +${entries.length - 1} more` : ''
  return `${first.exerciseName} ${first.sets}×${first.reps}${load}${rest}`
}

/**
 * The one action the home screen exists for, reduced to a single tap — and the redesign's promoted
 * primary CTA: a full-width pill rather than a plain button, so it reads as the thing to do here
 * rather than one card among several.
 *
 * It replaced a button that navigated to a form. That form was the product's central problem: after
 * a hard session nobody wants a second job, so most people never recorded anything and the whole
 * engine — XP, records, streaks, leaderboards — ran on air for them.
 *
 * The session is proposed server-side (see SessionProposalPolicy) from either the member's plan or
 * what they actually lifted last time, and shown ON the button rather than behind it: confirming
 * blind is how a member ends up with a record they never set. "Something different" is kept
 * deliberately quiet — it's the exception, and it's still the whole logger when they want it.
 *
 * The design reference's mockup title reads "Start {Workout Name}" with a "{n} exercises · {duration}"
 * subtitle — this app has no workout-template name to show for a repeat-last-session or starter
 * proposal (only a trainer plan is named), and no duration estimate exists anywhere in the product.
 * Rather than invent either, the title stays SESSION_SOURCE_LABEL (already honest about where the
 * session came from) and the subtitle stays the real exercise summary, exactly as before this restyle.
 */
export function ConfirmSessionButton({ onLogged }: { onLogged: (result: MyWorkoutResult) => void }) {
  const proposal = useMyNextSession()
  const logWorkout = useLogMyWorkout()

  const entries = proposal.data?.entries ?? []
  const canConfirm = proposal.data?.canConfirm ?? false

  const confirm = () => {
    logWorkout.mutate(
      entries.map((e) => ({
        exerciseId: e.exerciseId,
        setsCompleted: e.sets,
        repsCompleted: e.reps,
        weightKg: e.weightKg,
      })),
      {
        onSuccess: onLogged,
        onError: () => toast.error("Couldn't save that workout."),
      },
    )
  }

  // No proposal at all — no plan, no history, no exercise catalogue. Fall back to the full logger
  // rather than showing a button that can't do anything.
  if (!proposal.isLoading && !canConfirm) {
    return (
      <Button asChild className="h-16 w-full rounded-2xl text-base">
        <Link to="/log-activity">
          <Dumbbell className="size-5" />
          Log today's workout
        </Link>
      </Button>
    )
  }

  return (
    <div className="space-y-2">
      <button
        type="button"
        disabled={proposal.isLoading || logWorkout.isPending}
        onClick={confirm}
        className="flex w-full items-center gap-4 rounded-[20px] bg-primary p-3 text-left text-primary-foreground shadow-[0_8px_22px_-6px_var(--primary)] transition-transform disabled:opacity-70 not-disabled:hover:scale-[1.01]"
      >
        <span className="flex size-11 shrink-0 items-center justify-center rounded-2xl bg-primary-foreground/10">
          {logWorkout.isPending ? (
            <Loader2 className="size-5 animate-spin" />
          ) : (
            <Dumbbell className="size-5" />
          )}
        </span>
        <span className="min-w-0 flex-1">
          <span className="block truncate font-display text-lg font-bold">
            {proposal.isLoading ? 'Checking your plan…' : SESSION_SOURCE_LABEL[proposal.data!.source]}
          </span>
          {!proposal.isLoading && (
            <span className="block truncate text-sm opacity-80">{summarise(entries)}</span>
          )}
        </span>
        <ChevronRight className="size-5 shrink-0 opacity-70" />
      </button>

      <Button asChild variant="ghost" className="h-11 w-full text-sm text-muted-foreground">
        <Link to="/log-activity">Something different</Link>
      </Button>
    </div>
  )
}
