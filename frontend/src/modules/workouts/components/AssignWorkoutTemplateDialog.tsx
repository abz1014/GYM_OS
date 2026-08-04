import { useState } from 'react'
import { ClipboardList, Loader2 } from 'lucide-react'
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
import { Textarea } from '@/components/ui/textarea'
import { useAssignWorkoutTemplate, useWorkoutTemplatesList } from '@/modules/workouts/api/workoutsApi'

export function AssignWorkoutTemplateDialog({ memberId }: { memberId: string }) {
  const [open, setOpen] = useState(false)
  const [workoutTemplateId, setWorkoutTemplateId] = useState('')
  const [startDate, setStartDate] = useState('')
  const [endDate, setEndDate] = useState('')
  const [notes, setNotes] = useState('')

  const { data: templates } = useWorkoutTemplatesList()
  const assignTemplate = useAssignWorkoutTemplate()

  const reset = () => {
    setWorkoutTemplateId('')
    setStartDate('')
    setEndDate('')
    setNotes('')
  }

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!workoutTemplateId) {
      toast.error('Select a workout template.')
      return
    }
    if (!startDate) {
      toast.error('Set a start date.')
      return
    }

    assignTemplate.mutate(
      { memberId, workoutTemplateId, startDate, endDate: endDate || undefined, notes: notes || undefined },
      {
        onSuccess: () => {
          toast.success('Workout plan assigned.')
          setOpen(false)
          reset()
        },
        onError: () => toast.error('Could not assign workout plan.'),
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button size="sm" variant="outline">
          <ClipboardList />
          Assign Plan
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Assign workout plan</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <Label>Workout template</Label>
            <Select value={workoutTemplateId} onValueChange={setWorkoutTemplateId}>
              <SelectTrigger className="w-full">
                <SelectValue placeholder="Select a template" />
              </SelectTrigger>
              <SelectContent>
                {templates?.map((t) => (
                  <SelectItem key={t.id} value={t.id}>
                    {t.name} ({t.exerciseCount} exercises)
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="assignStartDate">Start date</Label>
              <Input id="assignStartDate" type="date" required value={startDate} onChange={(e) => setStartDate(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="assignEndDate">End date (optional)</Label>
              <Input id="assignEndDate" type="date" value={endDate} onChange={(e) => setEndDate(e.target.value)} />
            </div>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="assignNotes">Notes (optional)</Label>
            <Textarea id="assignNotes" value={notes} onChange={(e) => setNotes(e.target.value)} />
          </div>
          <DialogFooter>
            <Button type="submit" disabled={assignTemplate.isPending}>
              {assignTemplate.isPending && <Loader2 className="size-4 animate-spin" />}
              Assign Plan
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
