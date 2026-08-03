import { useState } from 'react'
import { CalendarPlus, Loader2 } from 'lucide-react'
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
import { useScheduleSession } from '@/modules/trainers/api/trainersApi'
import type { TrainerAssignment } from '@/modules/trainers/api/trainersApi'

export function ScheduleSessionDialog({ trainerId, assignments }: { trainerId: string; assignments: TrainerAssignment[] }) {
  const [open, setOpen] = useState(false)
  const [assignmentId, setAssignmentId] = useState('')
  const [scheduledAt, setScheduledAt] = useState('')
  const [durationMinutes, setDurationMinutes] = useState('60')
  const [notes, setNotes] = useState('')

  const activeAssignments = assignments.filter((a) => a.isActive)
  const scheduleSession = useScheduleSession(trainerId)

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!assignmentId || !scheduledAt) {
      toast.error('Select a client and a date/time.')
      return
    }

    scheduleSession.mutate(
      {
        assignmentId,
        scheduledAt: new Date(scheduledAt).toISOString(),
        durationMinutes: Number(durationMinutes),
        notes: notes || undefined,
      },
      {
        onSuccess: () => {
          toast.success('Session scheduled.')
          setOpen(false)
          setAssignmentId('')
          setScheduledAt('')
          setNotes('')
        },
        onError: () => toast.error('Could not schedule session.'),
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button size="sm" variant="outline">
          <CalendarPlus />
          Schedule Session
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Schedule a training session</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <Label>Client</Label>
            <Select value={assignmentId} onValueChange={setAssignmentId}>
              <SelectTrigger className="w-full">
                <SelectValue placeholder="Select client" />
              </SelectTrigger>
              <SelectContent>
                {activeAssignments.map((a) => (
                  <SelectItem key={a.id} value={a.id}>
                    {a.memberName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {activeAssignments.length === 0 && (
              <p className="text-xs text-muted-foreground">This trainer has no active clients to schedule with.</p>
            )}
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="scheduledAt">Date &amp; time</Label>
              <Input
                id="scheduledAt"
                type="datetime-local"
                required
                value={scheduledAt}
                onChange={(e) => setScheduledAt(e.target.value)}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="durationMinutes">Duration (minutes)</Label>
              <Input
                id="durationMinutes"
                type="number"
                min={15}
                max={240}
                required
                value={durationMinutes}
                onChange={(e) => setDurationMinutes(e.target.value)}
              />
            </div>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="notes">Notes (optional)</Label>
            <Textarea id="notes" value={notes} onChange={(e) => setNotes(e.target.value)} />
          </div>
          <DialogFooter>
            <Button type="submit" disabled={scheduleSession.isPending || !assignmentId}>
              {scheduleSession.isPending && <Loader2 className="size-4 animate-spin" />}
              Schedule
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
