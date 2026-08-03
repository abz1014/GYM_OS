import { useState } from 'react'
import { Loader2, Plus } from 'lucide-react'
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
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { useAddLeadActivity, type LeadActivityType } from '@/modules/crm/api/crmApi'

const TYPES: LeadActivityType[] = ['Call', 'Email', 'Meeting', 'Note', 'TrialScheduled']

export function AddLeadActivityDialog({ leadId }: { leadId: string }) {
  const [open, setOpen] = useState(false)
  const [type, setType] = useState<LeadActivityType>('Call')
  const [notes, setNotes] = useState('')
  const [dueDate, setDueDate] = useState('')

  const addActivity = useAddLeadActivity(leadId)

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!notes.trim()) {
      toast.error('Enter some notes.')
      return
    }

    addActivity.mutate(
      { type, notes, dueDate: dueDate ? new Date(dueDate).toISOString() : undefined },
      {
        onSuccess: () => {
          toast.success('Activity logged.')
          setOpen(false)
          setNotes('')
          setDueDate('')
        },
        onError: () => toast.error('Could not log activity.'),
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button size="sm" variant="outline">
          <Plus />
          Log Activity
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Log an activity</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <Label>Type</Label>
            <Select value={type} onValueChange={(v) => setType(v as LeadActivityType)}>
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
            <Label htmlFor="notes">Notes</Label>
            <Textarea id="notes" required value={notes} onChange={(e) => setNotes(e.target.value)} />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="dueDate">Follow-up due (optional)</Label>
            <Input id="dueDate" type="datetime-local" value={dueDate} onChange={(e) => setDueDate(e.target.value)} />
          </div>
          <DialogFooter>
            <Button type="submit" disabled={addActivity.isPending}>
              {addActivity.isPending && <Loader2 className="size-4 animate-spin" />}
              Log Activity
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
