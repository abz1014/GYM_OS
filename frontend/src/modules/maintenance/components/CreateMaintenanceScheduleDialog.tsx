import { useState } from 'react'
import { CalendarClock, Loader2 } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { useAssetsList } from '@/modules/equipment/api/equipmentApi'
import { useCreateMaintenanceSchedule } from '@/modules/maintenance/api/maintenanceApi'
import { useUiStore } from '@/stores/uiStore'

export function CreateMaintenanceScheduleDialog() {
  const [open, setOpen] = useState(false)
  const [assetId, setAssetId] = useState('')
  const [recurrenceRule, setRecurrenceRule] = useState('')
  const [nextDueDate, setNextDueDate] = useState('')

  const branchId = useUiStore((s) => s.selectedBranchId)
  const { data: assetsPage } = useAssetsList({ branchId, page: 1, pageSize: 100 })
  const assets = assetsPage?.items
  const createSchedule = useCreateMaintenanceSchedule()

  const reset = () => {
    setAssetId('')
    setRecurrenceRule('')
    setNextDueDate('')
  }

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!assetId) {
      toast.error('Select an asset.')
      return
    }
    if (!nextDueDate) {
      toast.error('Set the next due date.')
      return
    }

    createSchedule.mutate(
      { assetId, recurrenceRule, nextDueDate },
      {
        onSuccess: () => {
          toast.success('Maintenance schedule created.')
          setOpen(false)
          reset()
        },
        onError: () => toast.error('Could not create maintenance schedule.'),
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button className="rounded-xl">
          <CalendarClock />
          New schedule
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Create recurring maintenance schedule</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <Label>Asset</Label>
            <Select value={assetId} onValueChange={setAssetId}>
              <SelectTrigger className="w-full">
                <SelectValue placeholder="Select asset" />
              </SelectTrigger>
              <SelectContent>
                {assets?.map((a) => (
                  <SelectItem key={a.id} value={a.id}>
                    {a.name} ({a.assetTag})
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="recurrenceRule">Recurrence</Label>
            <Input
              id="recurrenceRule"
              required
              placeholder="e.g. Every 3 months"
              value={recurrenceRule}
              onChange={(e) => setRecurrenceRule(e.target.value)}
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="nextDueDate">Next due date</Label>
            <Input
              id="nextDueDate"
              type="date"
              required
              className="tabular-nums"
              value={nextDueDate}
              onChange={(e) => setNextDueDate(e.target.value)}
            />
          </div>
          <DialogFooter>
            <Button type="submit" className="rounded-xl" disabled={createSchedule.isPending}>
              {createSchedule.isPending && <Loader2 className="size-4 animate-spin" />}
              Create
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
