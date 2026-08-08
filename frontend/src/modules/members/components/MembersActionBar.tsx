import { useState } from 'react'
import { toast } from 'sonner'
import { Snowflake, StickyNote, X } from 'lucide-react'

import { Button } from '@/components/ui/button'
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { useAuthStore } from '@/stores/authStore'
import {
  useBatchAddMemberNote, useBatchFreezeMemberships, type BatchResult,
} from '@/modules/members/api/membersApi'

/**
 * Report a batch result honestly, including the half that did not happen.
 *
 * A batch that says "20 members frozen" when six were skipped is the single worst outcome this
 * feature can produce, because nobody goes back to check a success message. So a mixed result is a
 * warning, not a success, and it names the first few reasons — which are per-plan and per-member, so
 * a count alone would leave staff unable to act on the failures.
 */
function reportBatch(result: BatchResult, verb: string) {
  if (result.failed === 0) {
    toast.success(`${result.succeeded} ${result.succeeded === 1 ? 'member' : 'members'} ${verb}.`)
    return
  }

  const reasons = [...new Set(result.outcomes.filter((o) => !o.succeeded && o.reason).map((o) => o.reason!))]
  const shown = reasons.slice(0, 2).join(' ')
  const more = reasons.length > 2 ? ` (+${reasons.length - 2} more reasons)` : ''

  if (result.succeeded === 0) {
    toast.error(`Nothing ${verb}. ${shown}${more}`)
    return
  }

  toast.warning(`${result.succeeded} of ${result.requested} ${verb}. ${result.failed} skipped: ${shown}${more}`)
}

/** Today, and a fortnight out — the shape of freeze most members ask for, still fully editable. */
function defaultRange() {
  const start = new Date()
  const end = new Date()
  end.setDate(end.getDate() + 14)
  const iso = (d: Date) => d.toISOString().slice(0, 10)
  return { start: iso(start), end: iso(end) }
}

/**
 * The selection bar over the members list.
 *
 * Appears only with a selection, and each action appears only if the person holds the permission the
 * matching endpoint requires — `members.manage-membership` for a freeze, `members.update` for a note.
 * That gating is the point: this console currently shows several roles buttons that 403 on click, and
 * a BULK button that 403s would be that failure multiplied by the size of the selection.
 */
export function MembersActionBar({
  selectedIds, onClear,
}: {
  selectedIds: string[]
  onClear: () => void
}) {
  const hasPermission = useAuthStore((s) => s.hasPermission)
  const [freezeOpen, setFreezeOpen] = useState(false)
  const [noteOpen, setNoteOpen] = useState(false)
  const [range, setRange] = useState(defaultRange)
  const [note, setNote] = useState('')

  const freeze = useBatchFreezeMemberships()
  const addNote = useBatchAddMemberNote()

  const canFreeze = hasPermission('members.manage_membership')
  const canNote = hasPermission('members.update')

  if (selectedIds.length === 0 || (!canFreeze && !canNote)) {
    return null
  }

  const count = selectedIds.length

  const submitFreeze = () => {
    freeze.mutate(
      { memberIds: selectedIds, freezeStartDate: range.start, freezeEndDate: range.end },
      {
        onSuccess: (result) => {
          reportBatch(result, 'frozen')
          setFreezeOpen(false)
          // Only clear when every member went through. A partial result leaves the selection intact
          // so the person can see what they were acting on and retry the ones that were skipped.
          if (result.failed === 0) onClear()
        },
        onError: () => toast.error('Could not freeze the selected memberships.'),
      },
    )
  }

  const submitNote = () => {
    addNote.mutate(
      { memberIds: selectedIds, note },
      {
        onSuccess: (result) => {
          reportBatch(result, 'noted')
          setNoteOpen(false)
          setNote('')
          if (result.failed === 0) onClear()
        },
        onError: () => toast.error('Could not add the note.'),
      },
    )
  }

  return (
    <>
      <div className="sticky bottom-3 z-20 flex flex-wrap items-center gap-3 rounded-panel border border-border bg-card p-3 edge-light-soft">
        <span className="text-sm font-medium tabular-nums">
          {count} selected
        </span>

        <div className="ml-auto flex flex-wrap items-center gap-2">
          {canFreeze && (
            <Button variant="outline" size="sm" className="press" onClick={() => setFreezeOpen(true)}>
              <Snowflake className="size-4" aria-hidden />
              Freeze
            </Button>
          )}
          {canNote && (
            <Button variant="outline" size="sm" className="press" onClick={() => setNoteOpen(true)}>
              <StickyNote className="size-4" aria-hidden />
              Add note
            </Button>
          )}
          <Button variant="ghost" size="sm" className="press" onClick={onClear} aria-label="Clear selection">
            <X className="size-4" aria-hidden />
          </Button>
        </div>
      </div>

      <Dialog open={freezeOpen} onOpenChange={setFreezeOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Freeze {count} {count === 1 ? 'membership' : 'memberships'}</DialogTitle>
            <DialogDescription>
              Each plan sets its own maximum freeze length, so members on a plan that allows less than
              this will be skipped and listed back to you.
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="freeze-start">Start</Label>
              <Input
                id="freeze-start" type="date" value={range.start}
                onChange={(e) => setRange((r) => ({ ...r, start: e.target.value }))}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="freeze-end">End</Label>
              <Input
                id="freeze-end" type="date" value={range.end} min={range.start}
                onChange={(e) => setRange((r) => ({ ...r, end: e.target.value }))}
              />
            </div>
          </div>

          <DialogFooter>
            <Button variant="ghost" onClick={() => setFreezeOpen(false)}>Cancel</Button>
            <Button
              className="press"
              onClick={submitFreeze}
              disabled={freeze.isPending || !range.start || !range.end || range.end < range.start}
            >
              {freeze.isPending ? 'Freezing…' : `Freeze ${count}`}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={noteOpen} onOpenChange={setNoteOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Add a note to {count} {count === 1 ? 'member' : 'members'}</DialogTitle>
            <DialogDescription>
              A staff note on the member's record, saved against your name. This is not a medical note.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-2">
            <Label htmlFor="batch-note">Note</Label>
            <Textarea
              id="batch-note" value={note} maxLength={2000} rows={4}
              placeholder="Called about the September renewal."
              onChange={(e) => setNote(e.target.value)}
            />
          </div>

          <DialogFooter>
            <Button variant="ghost" onClick={() => setNoteOpen(false)}>Cancel</Button>
            <Button className="press" onClick={submitNote} disabled={addNote.isPending || note.trim().length === 0}>
              {addNote.isPending ? 'Saving…' : `Add to ${count}`}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  )
}
