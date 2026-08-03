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
import { useAddMeasurement } from '@/modules/members/api/membersApi'

function toNumberOrNull(value: string) {
  return value === '' ? null : Number(value)
}

export function AddMeasurementDialog({ memberId }: { memberId: string }) {
  const [open, setOpen] = useState(false)
  const [measuredOn, setMeasuredOn] = useState(new Date().toISOString().slice(0, 10))
  const [weightKg, setWeightKg] = useState('')
  const [bodyFatPercentage, setBodyFatPercentage] = useState('')
  const [chestCm, setChestCm] = useState('')
  const [waistCm, setWaistCm] = useState('')
  const [hipCm, setHipCm] = useState('')
  const [armCm, setArmCm] = useState('')
  const [thighCm, setThighCm] = useState('')
  const [notes, setNotes] = useState('')

  const addMeasurement = useAddMeasurement(memberId)

  const reset = () => {
    setWeightKg('')
    setBodyFatPercentage('')
    setChestCm('')
    setWaistCm('')
    setHipCm('')
    setArmCm('')
    setThighCm('')
    setNotes('')
  }

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    addMeasurement.mutate(
      {
        measuredOn,
        weightKg: toNumberOrNull(weightKg),
        bodyFatPercentage: toNumberOrNull(bodyFatPercentage),
        chestCm: toNumberOrNull(chestCm),
        waistCm: toNumberOrNull(waistCm),
        hipCm: toNumberOrNull(hipCm),
        armCm: toNumberOrNull(armCm),
        thighCm: toNumberOrNull(thighCm),
        notes: notes || null,
      },
      {
        onSuccess: () => {
          toast.success('Measurement logged.')
          setOpen(false)
          reset()
        },
        onError: () => toast.error('Could not log measurement.'),
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button size="sm" variant="outline">
          <Plus />
          Log Measurement
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Log a measurement</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="measuredOn">Date</Label>
            <Input id="measuredOn" type="date" required value={measuredOn} onChange={(e) => setMeasuredOn(e.target.value)} />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="weightKg">Weight (kg)</Label>
              <Input id="weightKg" type="number" min={0} step="0.1" value={weightKg} onChange={(e) => setWeightKg(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="bodyFat">Body fat (%)</Label>
              <Input
                id="bodyFat"
                type="number"
                min={0}
                step="0.1"
                value={bodyFatPercentage}
                onChange={(e) => setBodyFatPercentage(e.target.value)}
              />
            </div>
          </div>
          <div className="grid grid-cols-3 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="chestCm">Chest (cm)</Label>
              <Input id="chestCm" type="number" min={0} step="0.1" value={chestCm} onChange={(e) => setChestCm(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="waistCm">Waist (cm)</Label>
              <Input id="waistCm" type="number" min={0} step="0.1" value={waistCm} onChange={(e) => setWaistCm(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="hipCm">Hip (cm)</Label>
              <Input id="hipCm" type="number" min={0} step="0.1" value={hipCm} onChange={(e) => setHipCm(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="armCm">Arm (cm)</Label>
              <Input id="armCm" type="number" min={0} step="0.1" value={armCm} onChange={(e) => setArmCm(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="thighCm">Thigh (cm)</Label>
              <Input id="thighCm" type="number" min={0} step="0.1" value={thighCm} onChange={(e) => setThighCm(e.target.value)} />
            </div>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="measurementNotes">Notes (optional)</Label>
            <Input id="measurementNotes" value={notes} onChange={(e) => setNotes(e.target.value)} />
          </div>
          <DialogFooter>
            <Button type="submit" disabled={addMeasurement.isPending}>
              {addMeasurement.isPending && <Loader2 className="size-4 animate-spin" />}
              Log Measurement
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
