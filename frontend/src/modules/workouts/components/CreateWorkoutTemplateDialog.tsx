import { useState } from 'react'
import { Loader2, Plus, X } from 'lucide-react'
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
import { useCreateWorkoutTemplate, useExercisesList } from '@/modules/workouts/api/workoutsApi'

interface DraftExercise {
  exerciseId: string
  exerciseName: string
  setsCount: number
  repsCount: number
}

export function CreateWorkoutTemplateDialog() {
  const [open, setOpen] = useState(false)
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [selectedExerciseId, setSelectedExerciseId] = useState('')
  const [draftExercises, setDraftExercises] = useState<DraftExercise[]>([])

  const { data: exercises } = useExercisesList()
  const createTemplate = useCreateWorkoutTemplate()

  const addExercise = () => {
    const exercise = exercises?.find((e) => e.id === selectedExerciseId)
    if (!exercise) return
    setDraftExercises((prev) => [...prev, { exerciseId: exercise.id, exerciseName: exercise.name, setsCount: 3, repsCount: 10 }])
    setSelectedExerciseId('')
  }

  const removeExercise = (index: number) => setDraftExercises((prev) => prev.filter((_, i) => i !== index))

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (draftExercises.length === 0) {
      toast.error('Add at least one exercise.')
      return
    }

    createTemplate.mutate(
      {
        name,
        description: description || undefined,
        exercises: draftExercises.map((d, i) => ({
          exerciseId: d.exerciseId,
          setsCount: d.setsCount,
          repsCount: d.repsCount,
          orderIndex: i,
        })),
      },
      {
        onSuccess: () => {
          toast.success('Template created.')
          setOpen(false)
          setName('')
          setDescription('')
          setDraftExercises([])
        },
        onError: () => toast.error('Could not create template.'),
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button>
          <Plus />
          New Template
        </Button>
      </DialogTrigger>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Build a workout template</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="name">Name</Label>
            <Input id="name" required value={name} onChange={(e) => setName(e.target.value)} />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="description">Description (optional)</Label>
            <Input id="description" value={description} onChange={(e) => setDescription(e.target.value)} />
          </div>

          <div className="space-y-2">
            <Label>Exercises</Label>
            <div className="flex gap-2">
              <Select value={selectedExerciseId} onValueChange={setSelectedExerciseId}>
                <SelectTrigger className="w-full">
                  <SelectValue placeholder="Select exercise" />
                </SelectTrigger>
                <SelectContent>
                  {exercises?.map((ex) => (
                    <SelectItem key={ex.id} value={ex.id}>
                      {ex.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <Button type="button" variant="outline" onClick={addExercise}>
                Add
              </Button>
            </div>

            {draftExercises.length > 0 && (
              <div className="divide-y divide-border rounded-xl border border-border">
                {draftExercises.map((d, i) => (
                  <div key={i} className="flex items-center justify-between gap-2 px-3 py-2 text-sm">
                    <span className="flex-1">{d.exerciseName}</span>
                    <Input
                      type="number"
                      min={1}
                      className="w-16"
                      value={d.setsCount}
                      onChange={(e) =>
                        setDraftExercises((prev) =>
                          prev.map((x, idx) => (idx === i ? { ...x, setsCount: Number(e.target.value) } : x))
                        )
                      }
                    />
                    <span className="text-muted-foreground">sets ×</span>
                    <Input
                      type="number"
                      min={1}
                      className="w-16"
                      value={d.repsCount}
                      onChange={(e) =>
                        setDraftExercises((prev) =>
                          prev.map((x, idx) => (idx === i ? { ...x, repsCount: Number(e.target.value) } : x))
                        )
                      }
                    />
                    <span className="text-muted-foreground">reps</span>
                    <button type="button" onClick={() => removeExercise(i)} className="text-muted-foreground hover:text-destructive">
                      <X className="size-4" />
                    </button>
                  </div>
                ))}
              </div>
            )}
          </div>

          <DialogFooter>
            <Button type="submit" disabled={createTemplate.isPending}>
              {createTemplate.isPending && <Loader2 className="size-4 animate-spin" />}
              Create Template
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
