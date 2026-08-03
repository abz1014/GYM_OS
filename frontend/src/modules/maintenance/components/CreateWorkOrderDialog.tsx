import { useState } from 'react'
import { Loader2, Plus } from 'lucide-react'
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
import { useCreateWorkOrder, type WorkOrderPriority, type WorkOrderType } from '@/modules/maintenance/api/maintenanceApi'
import { useUiStore } from '@/stores/uiStore'

const TYPES: WorkOrderType[] = ['Preventive', 'Corrective']
const PRIORITIES: WorkOrderPriority[] = ['Low', 'Medium', 'High', 'Critical']

export function CreateWorkOrderDialog() {
  const [open, setOpen] = useState(false)
  const [assetId, setAssetId] = useState('')
  const [type, setType] = useState<WorkOrderType>('Corrective')
  const [priority, setPriority] = useState<WorkOrderPriority>('Medium')
  const [title, setTitle] = useState('')
  const [scheduledDate, setScheduledDate] = useState('')

  const branchId = useUiStore((s) => s.selectedBranchId)
  const { data: assets } = useAssetsList({ branchId })
  const createWorkOrder = useCreateWorkOrder()

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!assetId) {
      toast.error('Select an asset.')
      return
    }

    createWorkOrder.mutate(
      { assetId, type, priority, title, scheduledDate: scheduledDate || undefined },
      {
        onSuccess: () => {
          toast.success('Work order created.')
          setOpen(false)
          setTitle('')
          setAssetId('')
          setScheduledDate('')
        },
        onError: () => toast.error('Could not create work order.'),
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button>
          <Plus />
          New Work Order
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Create work order</DialogTitle>
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
            <Label htmlFor="title">Title</Label>
            <Input id="title" required value={title} onChange={(e) => setTitle(e.target.value)} />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label>Type</Label>
              <Select value={type} onValueChange={(v) => setType(v as WorkOrderType)}>
                <SelectTrigger className="w-full">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {TYPES.map((t) => (
                    <SelectItem key={t} value={t}>
                      {t}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1.5">
              <Label>Priority</Label>
              <Select value={priority} onValueChange={(v) => setPriority(v as WorkOrderPriority)}>
                <SelectTrigger className="w-full">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {PRIORITIES.map((p) => (
                    <SelectItem key={p} value={p}>
                      {p}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="scheduledDate">Scheduled date (optional)</Label>
            <Input id="scheduledDate" type="date" value={scheduledDate} onChange={(e) => setScheduledDate(e.target.value)} />
          </div>
          <DialogFooter>
            <Button type="submit" disabled={createWorkOrder.isPending}>
              {createWorkOrder.isPending && <Loader2 className="size-4 animate-spin" />}
              Create
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
