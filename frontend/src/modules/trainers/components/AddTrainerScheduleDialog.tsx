import { useState } from 'react'
import { CalendarClock, Loader2 } from 'lucide-react'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
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
import { useAddTrainerSchedule } from '@/modules/trainers/api/trainersApi'

const DAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']

export function AddTrainerScheduleDialog({ trainerId }: { trainerId: string }) {
  const [open, setOpen] = useState(false)
  const [dayOfWeek, setDayOfWeek] = useState('Monday')
  const [startTime, setStartTime] = useState('09:00')
  const [endTime, setEndTime] = useState('17:00')
  const [isAvailable, setIsAvailable] = useState(true)

  const addSchedule = useAddTrainerSchedule(trainerId)

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    addSchedule.mutate(
      { dayOfWeek, startTime, endTime, isAvailable },
      {
        onSuccess: () => {
          toast.success('Schedule slot added.')
          setOpen(false)
        },
        onError: () => toast.error('Could not add schedule slot — check the end time is after the start time.'),
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button size="sm" variant="outline" className="rounded-xl">
          <CalendarClock />
          Add Schedule Slot
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Add weekly schedule slot</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <Label>Day of week</Label>
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
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="startTime">Start time</Label>
              <Input id="startTime" type="time" required value={startTime} onChange={(e) => setStartTime(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="endTime">End time</Label>
              <Input id="endTime" type="time" required value={endTime} onChange={(e) => setEndTime(e.target.value)} />
            </div>
          </div>
          <div className="flex items-center gap-2">
            <Checkbox id="isAvailable" checked={isAvailable} onCheckedChange={(v) => setIsAvailable(v === true)} />
            <Label htmlFor="isAvailable" className="font-normal">
              Available for new clients during this slot
            </Label>
          </div>
          <DialogFooter>
            <Button type="submit" disabled={addSchedule.isPending}>
              {addSchedule.isPending && <Loader2 className="size-4 animate-spin" />}
              Add Slot
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
