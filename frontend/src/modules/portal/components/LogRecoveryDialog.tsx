import { useState } from 'react'
import { Armchair, Footprints, Moon, StretchHorizontal } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { cn } from '@/lib/utils'
import { useLogMyRecovery, type MyRecoveryToday, type RecoveryKind } from '@/modules/portal/api/portalApi'

/**
 * Logging a recovery day.
 *
 * The button this replaces posted `{ kind: 'RestDay', notes: null }` and nothing else, which threw
 * away most of what the endpoint accepts: three of the four kinds were unreachable, and a member
 * could never write a note. It also called itself "Log recovery day" while only ever logging a rest
 * day.
 *
 * The kinds are presented as equals on purpose. RecoveryKind's own doc is explicit that "All count
 * equally toward the 'rest logged' signal ... the kind is for the member's own record, not a
 * different reward", so ordering them by value or implying one earns more would be a lie told in
 * layout. They differ in meaning, not in payoff.
 */
const KINDS: { value: RecoveryKind; label: string; hint: string; icon: typeof Moon }[] = [
  { value: 'RestDay', label: 'Full rest', hint: 'No training today', icon: Moon },
  { value: 'ActiveRecovery', label: 'Active recovery', hint: 'A walk, a swim, something easy', icon: Footprints },
  { value: 'Mobility', label: 'Mobility', hint: 'Joints and range of motion', icon: Armchair },
  { value: 'Stretching', label: 'Stretching', hint: 'Held stretches, cool-down work', icon: StretchHorizontal },
]

const KIND_LABEL: Record<RecoveryKind, string> = {
  RestDay: 'Full rest',
  ActiveRecovery: 'Active recovery',
  Mobility: 'Mobility',
  Stretching: 'Stretching',
}

/** Recovery XP is a flat 10, once per day — XpPolicy.cs. Stated because a reward the member cannot
    see is a reward that cannot motivate, and guessed because it is not: this is the real number. */
const RECOVERY_XP = 10

/**
 * Already logged today: show what it was instead of the button.
 *
 * The server allows one recovery log per day and returns the existing row for a second attempt, so
 * the old screen offered the button again and toasted "Recovery day logged." for a record it had not
 * created. Tapping five times produced five confirmations and one row.
 */
export function RecoveryLoggedToday({ today }: { today: MyRecoveryToday }) {
  return (
    <div className="rounded-panel border border-success/30 bg-success/5 px-3 py-2">
      <p className="text-sm font-medium text-success">
        {KIND_LABEL[today.kind]} logged today
      </p>
      {today.notes && <p className="mt-0.5 text-sm text-muted-foreground">“{today.notes}”</p>}
    </div>
  )
}

export function LogRecoveryDialog({ disabled }: { disabled?: boolean }) {
  const [open, setOpen] = useState(false)
  const [kind, setKind] = useState<RecoveryKind>('RestDay')
  const [notes, setNotes] = useState('')
  const logRecovery = useLogMyRecovery()

  function submit() {
    logRecovery.mutate(
      { kind, notes: notes.trim() === '' ? null : notes.trim() },
      {
        onSuccess: () => {
          // The query invalidation in useLogMyRecovery refetches recovery, so the card swaps to
          // RecoveryLoggedToday on its own — the toast does not have to carry the whole story.
          toast.success(`${KIND_LABEL[kind]} logged`, { description: `+${RECOVERY_XP} XP` })
          setOpen(false)
          setNotes('')
        },
        onError: () => toast.error("We couldn't log that. Try again in a moment."),
      },
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button size="sm" variant="outline" className="shrink-0" disabled={disabled}>
          <Moon className="mr-1 size-4" />
          Log recovery
        </Button>
      </DialogTrigger>

      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Log today's recovery</DialogTitle>
          <DialogDescription>
            Rest is training. Logging it keeps your week honest and earns {RECOVERY_XP} XP — once a
            day, whichever kind you pick.
          </DialogDescription>
        </DialogHeader>

        <div className="grid grid-cols-2 gap-2">
          {KINDS.map((k) => {
            const Icon = k.icon
            const selected = kind === k.value
            return (
              <button
                key={k.value}
                type="button"
                onClick={() => setKind(k.value)}
                aria-pressed={selected}
                className={cn(
                  'rounded-panel border p-3 text-left transition-colors duration-(--duration-micro)',
                  selected
                    ? 'border-primary bg-primary/10'
                    : 'border-border hover:bg-muted/50',
                )}
              >
                <Icon className={cn('size-4', selected ? 'text-primary' : 'text-muted-foreground')} />
                <span className="mt-1.5 block text-sm font-medium">{k.label}</span>
                <span className="block text-xs text-muted-foreground">{k.hint}</span>
              </button>
            )
          })}
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="recovery-notes">Notes (optional)</Label>
          <Textarea
            id="recovery-notes"
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            placeholder="Sore shoulder, easy walk instead…"
            rows={3}
          />
        </div>

        <DialogFooter>
          <DialogClose asChild>
            <Button variant="ghost" size="sm">
              Cancel
            </Button>
          </DialogClose>
          <Button size="sm" onClick={submit} disabled={logRecovery.isPending}>
            {logRecovery.isPending ? 'Logging…' : 'Log it'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
