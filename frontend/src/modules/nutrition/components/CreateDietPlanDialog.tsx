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
import { useCreateDietPlan } from '@/modules/nutrition/api/nutritionApi'

export function CreateDietPlanDialog({ memberId }: { memberId: string }) {
  const [open, setOpen] = useState(false)
  const [name, setName] = useState('')
  const [targetCalories, setTargetCalories] = useState('')
  const [targetProteinG, setTargetProteinG] = useState('')
  const [targetCarbsG, setTargetCarbsG] = useState('')
  const [targetFatG, setTargetFatG] = useState('')
  const [startDate, setStartDate] = useState('')
  const [endDate, setEndDate] = useState('')

  const createPlan = useCreateDietPlan()

  const reset = () => {
    setName('')
    setTargetCalories('')
    setTargetProteinG('')
    setTargetCarbsG('')
    setTargetFatG('')
    setStartDate('')
    setEndDate('')
  }

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!startDate) {
      toast.error('Set a start date.')
      return
    }

    createPlan.mutate(
      {
        memberId,
        name,
        targetCalories: targetCalories ? Number(targetCalories) : undefined,
        targetProteinG: targetProteinG ? Number(targetProteinG) : undefined,
        targetCarbsG: targetCarbsG ? Number(targetCarbsG) : undefined,
        targetFatG: targetFatG ? Number(targetFatG) : undefined,
        startDate,
        endDate: endDate || undefined,
      },
      {
        onSuccess: () => {
          toast.success('Diet plan created.')
          setOpen(false)
          reset()
        },
        onError: () => toast.error('Could not create diet plan.'),
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button size="sm" variant="outline">
          <ClipboardList />
          New Diet Plan
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Create diet plan</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="planName">Name</Label>
            <Input id="planName" required value={name} onChange={(e) => setName(e.target.value)} />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="targetCalories">Target calories (optional)</Label>
            <Input
              id="targetCalories"
              type="number"
              min={0}
              value={targetCalories}
              onChange={(e) => setTargetCalories(e.target.value)}
            />
          </div>
          <div className="grid grid-cols-3 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="targetProteinG">Protein g (optional)</Label>
              <Input id="targetProteinG" type="number" min={0} value={targetProteinG} onChange={(e) => setTargetProteinG(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="targetCarbsG">Carbs g (optional)</Label>
              <Input id="targetCarbsG" type="number" min={0} value={targetCarbsG} onChange={(e) => setTargetCarbsG(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="targetFatG">Fat g (optional)</Label>
              <Input id="targetFatG" type="number" min={0} value={targetFatG} onChange={(e) => setTargetFatG(e.target.value)} />
            </div>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="startDate">Start date</Label>
              <Input id="startDate" type="date" required value={startDate} onChange={(e) => setStartDate(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="endDate">End date (optional)</Label>
              <Input id="endDate" type="date" value={endDate} onChange={(e) => setEndDate(e.target.value)} />
            </div>
          </div>
          <DialogFooter>
            <Button type="submit" disabled={createPlan.isPending}>
              {createPlan.isPending && <Loader2 className="size-4 animate-spin" />}
              Create Plan
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
