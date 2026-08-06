import { Link } from 'react-router-dom'
import { Check, Dumbbell, Loader2 } from 'lucide-react'
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
 * The one action the home screen exists for, reduced to a single tap.
 *
 * It replaced a button that navigated to a form. That form was the product's central problem: after
 * a hard session nobody wants a second job, so most people never recorded anything and the whole
 * engine — XP, records, streaks, leaderboards — ran on air for them.
 *
 * The session is proposed server-side (see SessionProposalPolicy) from either the member's plan or
 * what they actually lifted last time, and shown ON the button rather than behind it: confirming
 * blind is how a member ends up with a record they never set. "Something different" is kept
 * deliberately quiet — it's the exception, and it's still the whole logger when they want it.
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
      <Button asChild className="h-16 w-full text-base">
        <Link to="/log-activity">
          <Dumbbell className="size-5" />
          Log today's workout
        </Link>
      </Button>
    )
  }

  return (
    <div className="space-y-2">
      <Button
        className="h-auto min-h-16 w-full flex-col gap-0.5 py-3 text-base"
        disabled={proposal.isLoading || logWorkout.isPending}
        onClick={confirm}
      >
        <span className="flex items-center gap-2 font-semibold">
          {logWorkout.isPending ? <Loader2 className="size-5 animate-spin" /> : <Check className="size-5" />}
          {proposal.isLoading ? 'Checking your plan…' : SESSION_SOURCE_LABEL[proposal.data!.source]}
        </span>
        {!proposal.isLoading && (
          <span className="text-xs font-normal opacity-90">{summarise(entries)}</span>
        )}
      </Button>

      <Button asChild variant="ghost" className="h-11 w-full text-sm text-muted-foreground">
        <Link to="/log-activity">Something different</Link>
      </Button>
    </div>
  )
}
