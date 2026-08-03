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
import { useCreateExercise } from '@/modules/workouts/api/workoutsApi'

export function CreateExerciseDialog() {
  const [open, setOpen] = useState(false)
  const [name, setName] = useState('')
  const [muscleGroup, setMuscleGroup] = useState('')
  const [equipment, setEquipment] = useState('')

  const createExercise = useCreateExercise()

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    createExercise.mutate(
      { name, muscleGroup: muscleGroup || undefined, equipment: equipment || undefined },
      {
        onSuccess: () => {
          toast.success('Exercise added.')
          setOpen(false)
          setName('')
          setMuscleGroup('')
          setEquipment('')
        },
        onError: () => toast.error('Could not add exercise.'),
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button>
          <Plus />
          Add Exercise
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Add exercise</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="name">Name</Label>
            <Input id="name" required value={name} onChange={(e) => setName(e.target.value)} />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="muscleGroup">Muscle group</Label>
              <Input id="muscleGroup" value={muscleGroup} onChange={(e) => setMuscleGroup(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="equipment">Equipment</Label>
              <Input id="equipment" value={equipment} onChange={(e) => setEquipment(e.target.value)} />
            </div>
          </div>
          <DialogFooter>
            <Button type="submit" disabled={createExercise.isPending}>
              {createExercise.isPending && <Loader2 className="size-4 animate-spin" />}
              Add
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
