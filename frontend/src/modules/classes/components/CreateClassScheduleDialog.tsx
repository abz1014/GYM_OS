import { useState } from 'react'
import { CalendarPlus, Loader2 } from 'lucide-react'
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
import { useClassTypes, useCreateClassSchedule } from '@/modules/classes/api/classesApi'
import { useTrainersList } from '@/modules/trainers/api/trainersApi'
import { useUiStore } from '@/stores/uiStore'

const DAYS = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday']
const NO_TRAINER = 'none'

export function CreateClassScheduleDialog() {
  const branchId = useUiStore((s) => s.selectedBranchId)
  const [open, setOpen] = useState(false)
  const [classTypeId, setClassTypeId] = useState('')
  const [trainerId, setTrainerId] = useState(NO_TRAINER)
  const [dayOfWeek, setDayOfWeek] = useState('Monday')
  const [startTime, setStartTime] = useState('18:00')
  const [duration, setDuration] = useState<number | ''>('')
  const [capacity, setCapacity] = useState<number | ''>('')
  const [location, setLocation] = useState('')

  const { data: classTypes } = useClassTypes()
  const { data: trainers } = useTrainersList(branchId)
  const createSchedule = useCreateClassSchedule()

  const reset = () => {
    setClassTypeId('')
    setTrainerId(NO_TRAINER)
    setDayOfWeek('Monday')
    setStartTime('18:00')
    setDuration('')
    setCapacity('')
    setLocation('')
  }

  // Prefill duration/capacity from the chosen type's defaults, but leave them editable.
  const handleTypeChange = (id: string) => {
    setClassTypeId(id)
    const type = classTypes?.find((t) => t.id === id)
    if (type) {
      setDuration(type.defaultDurationMinutes)
      setCapacity(type.defaultCapacity)
    }
  }

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!classTypeId) {
      toast.error('Pick a class type.')
      return
    }
    if (!branchId) {
      toast.error('Select a branch first.')
      return
    }

    createSchedule.mutate(
      {
        branchId,
        classTypeId,
        trainerId: trainerId === NO_TRAINER ? undefined : trainerId,
        dayOfWeek,
        startTime,
        durationMinutes: duration === '' ? undefined : duration,
        capacity: capacity === '' ? undefined : capacity,
        location: location || undefined,
      },
      {
        onSuccess: () => {
          toast.success('Class slot added to the schedule.')
          setOpen(false)
          reset()
        },
        onError: () => toast.error('Could not add class slot.'),
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button size="sm" className="rounded-xl">
          <CalendarPlus />
          New class slot
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Add a recurring class slot</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <Label>Class type</Label>
            <Select value={classTypeId} onValueChange={handleTypeChange}>
              <SelectTrigger className="w-full">
                <SelectValue placeholder="Select a class type" />
              </SelectTrigger>
              <SelectContent>
                {classTypes?.filter((t) => t.isActive).map((t) => (
                  <SelectItem key={t.id} value={t.id}>
                    {t.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label>Day</Label>
              <Select value={dayOfWeek} onValueChange={setDayOfWeek}>
                <SelectTrigger className="w-full">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {DAYS.map((d) => (
                    <SelectItem key={d} value={d}>
                      {d}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="classStartTime">Start time</Label>
              <Input
                id="classStartTime"
                type="time"
                required
                className="tabular-nums"
                value={startTime}
                onChange={(e) => setStartTime(e.target.value)}
              />
            </div>
          </div>

          <div className="space-y-1.5">
            <Label>Instructor (optional)</Label>
            <Select value={trainerId} onValueChange={setTrainerId}>
              <SelectTrigger className="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={NO_TRAINER}>No instructor assigned</SelectItem>
                {trainers?.map((t) => (
                  <SelectItem key={t.id} value={t.id}>
                    {t.fullName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="classDuration">Duration (min)</Label>
              <Input
                id="classDuration"
                type="number"
                min={5}
                max={480}
                className="tabular-nums"
                value={duration}
                onChange={(e) => setDuration(e.target.value === '' ? '' : Number(e.target.value))}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="classCapacity">Capacity</Label>
              <Input
                id="classCapacity"
                type="number"
                min={1}
                max={1000}
                className="tabular-nums"
                value={capacity}
                onChange={(e) => setCapacity(e.target.value === '' ? '' : Number(e.target.value))}
              />
            </div>
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="classLocation">Location (optional)</Label>
            <Input id="classLocation" value={location} onChange={(e) => setLocation(e.target.value)} placeholder="e.g. Studio A" />
          </div>

          <DialogFooter>
            <Button type="submit" className="rounded-xl" disabled={createSchedule.isPending}>
              {createSchedule.isPending && <Loader2 className="size-4 animate-spin" />}
              Add slot
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
