import { useEffect, useState } from 'react'
import { Minus, Plus } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { useSetMyWeeklyGoal } from '@/modules/portal/api/portalApi'

/** Mirrors WeeklyGoalPolicy on the server. The server validates too — this only keeps the ± buttons
 *  from offering a value it would reject. */
const MIN_GOAL = 1
const MAX_GOAL = 14

/**
 * Letting a member choose their own weekly target, because three sessions is a sensible default and
 * a bad universal rule: someone training twice a week is being told they failed every week, and
 * someone training six is handed a ring they close by Wednesday. A goal you didn't choose isn't
 * motivating — it's just someone else's expectation.
 */
export function WeeklyGoalDialog({
  open,
  onOpenChange,
  currentGoal,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  currentGoal: number
}) {
  const [goal, setGoal] = useState(currentGoal)
  const setWeeklyGoal = useSetMyWeeklyGoal()

  // Re-sync when reopened, so a cancelled edit doesn't linger as the starting point next time.
  useEffect(() => {
    if (open) setGoal(currentGoal)
  }, [open, currentGoal])

  const save = () => {
    setWeeklyGoal.mutate(goal, {
      onSuccess: () => {
        toast.success(`Weekly goal set to ${goal} ${goal === 1 ? 'session' : 'sessions'}`)
        onOpenChange(false)
      },
      onError: () => toast.error("Couldn't save your goal. Try again."),
    })
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-sm">
        <DialogHeader>
          <DialogTitle>Weekly goal</DialogTitle>
          <DialogDescription>How many sessions a week are you training for?</DialogDescription>
        </DialogHeader>

        <div className="flex items-center justify-center gap-5 py-4">
          <Button
            type="button"
            variant="outline"
            size="icon"
            className="size-12 shrink-0 rounded-full"
            aria-label="Decrease goal"
            disabled={goal <= MIN_GOAL}
            onClick={() => setGoal((g) => Math.max(MIN_GOAL, g - 1))}
          >
            <Minus className="size-5" />
          </Button>

          <span className="flex min-w-24 flex-col items-center">
            <span className="text-5xl leading-none font-bold tabular-nums">{goal}</span>
            <span className="mt-1 text-xs text-muted-foreground">
              {goal === 1 ? 'session / week' : 'sessions / week'}
            </span>
          </span>

          <Button
            type="button"
            variant="outline"
            size="icon"
            className="size-12 shrink-0 rounded-full"
            aria-label="Increase goal"
            disabled={goal >= MAX_GOAL}
            onClick={() => setGoal((g) => Math.min(MAX_GOAL, g + 1))}
          >
            <Plus className="size-5" />
          </Button>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button onClick={save} disabled={setWeeklyGoal.isPending}>
            {setWeeklyGoal.isPending ? 'Saving…' : 'Save goal'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
