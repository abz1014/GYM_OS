import { useState } from 'react'
import { CheckCircle2, Loader2 } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import { useVerifyWorkOrder } from '@/modules/maintenance/api/maintenanceApi'

export function VerifyWorkOrderDialog({ workOrderId, isScheduleLinked }: { workOrderId: string; isScheduleLinked: boolean }) {
  const [open, setOpen] = useState(false)
  const [cost, setCost] = useState('')
  const [notes, setNotes] = useState('')
  const [nextDueDate, setNextDueDate] = useState('')

  const verifyWorkOrder = useVerifyWorkOrder(workOrderId)

  const handleApprove = (e: React.FormEvent) => {
    e.preventDefault()
    if (isScheduleLinked && !nextDueDate) {
      toast.error('This work order is linked to a recurring schedule — set its next due date.')
      return
    }

    verifyWorkOrder.mutate(
      { approved: true, cost: cost ? Number(cost) : undefined, notes: notes || undefined, nextDueDate: nextDueDate || undefined },
      {
        onSuccess: () => {
          toast.success('Work order verified and closed.')
          setOpen(false)
        },
        onError: () => toast.error('Could not verify work order.'),
      }
    )
  }

  const handleReject = () => {
    verifyWorkOrder.mutate(
      { approved: false, notes: notes || undefined },
      {
        onSuccess: () => {
          toast.success('Sent back for more repair work.')
          setOpen(false)
        },
        onError: () => toast.error('Could not update work order.'),
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button className="rounded-xl">
          <CheckCircle2 />
          Verify
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Verify repair</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleApprove} className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="cost">Cost (optional)</Label>
            <Input
              id="cost"
              type="number"
              min={0}
              step="0.01"
              className="tabular-nums"
              value={cost}
              onChange={(e) => setCost(e.target.value)}
            />
            {/* No currency symbol or code beside this field: the work order endpoint neither takes
                nor returns one, so anything shown here would be a guess about how the gym bills. */}
          </div>
          {isScheduleLinked && (
            <div className="space-y-1.5">
              <Label htmlFor="nextDueDate">Next inspection due date</Label>
              <Input
                id="nextDueDate"
                type="date"
                required
                value={nextDueDate}
                onChange={(e) => setNextDueDate(e.target.value)}
              />
              <p className="text-xs text-muted-foreground">
                This work order is linked to a recurring schedule — approving it advances that schedule to this date.
              </p>
            </div>
          )}
          <div className="space-y-1.5">
            <Label htmlFor="notes">Verification notes (optional)</Label>
            <Textarea id="notes" value={notes} onChange={(e) => setNotes(e.target.value)} />
          </div>
          <DialogFooter className="gap-2 sm:justify-between">
            <Button
              type="button"
              variant="outline"
              className="rounded-xl"
              disabled={verifyWorkOrder.isPending}
              onClick={handleReject}
            >
              Send back for more repair
            </Button>
            <Button type="submit" className="rounded-xl" disabled={verifyWorkOrder.isPending}>
              {verifyWorkOrder.isPending && <Loader2 className="size-4 animate-spin" />}
              Approve &amp; close
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
