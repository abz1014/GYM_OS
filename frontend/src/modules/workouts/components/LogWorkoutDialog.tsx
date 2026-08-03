import { useEffect, useState } from 'react'
import { Loader2, Plus, Trash2 } from 'lucide-react'
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
import {
  useExercisesList,
  useLogWorkout,
  useWorkoutTemplate,
  useWorkoutTemplatesList,
} from '@/modules/workouts/api/workoutsApi'

interface EntryRow {
  exerciseId: string
  setsCompleted: string
  repsCompleted: string
  weightKg: string
}

const emptyRow: EntryRow = { exerciseId: '', setsCompleted: '3', repsCompleted: '10', weightKg: '' }

export function LogWorkoutDialog({ memberId }: { memberId: string }) {
  const [open, setOpen] = useState(false)
  const [templateId, setTemplateId] = useState('')
  const [rows, setRows] = useState<EntryRow[]>([{ ...emptyRow }])

  const { data: templates } = useWorkoutTemplatesList()
  const { data: exercises } = useExercisesList()
  const { data: template } = useWorkoutTemplate(templateId || undefined)
  const logWorkout = useLogWorkout()

  const applyTemplate = (id: string) => {
    setTemplateId(id)
    if (!id) {
      setRows([{ ...emptyRow }])
    }
  }

  useEffect(() => {
    if (template) {
      setRows(
        template.exercises.map((e) => ({
          exerciseId: e.exerciseId,
          setsCompleted: String(e.setsCount),
          repsCompleted: String(e.repsCount),
          weightKg: '',
        }))
      )
    }
  }, [template])

  const updateRow = (index: number, patch: Partial<EntryRow>) => {
    setRows((prev) => prev.map((r, i) => (i === index ? { ...r, ...patch } : r)))
  }

  const removeRow = (index: number) => setRows((prev) => prev.filter((_, i) => i !== index))

  const reset = () => {
    setTemplateId('')
    setRows([{ ...emptyRow }])
  }

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    const entries = rows.filter((r) => r.exerciseId)
    if (entries.length === 0) {
      toast.error('Add at least one exercise.')
      return
    }

    logWorkout.mutate(
      {
        memberId,
        workoutTemplateId: templateId || undefined,
        entries: entries.map((r) => ({
          exerciseId: r.exerciseId,
          setsCompleted: Number(r.setsCompleted) || 0,
          repsCompleted: Number(r.repsCompleted) || 0,
          weightKg: r.weightKg ? Number(r.weightKg) : undefined,
        })),
      },
      {
        onSuccess: () => {
          toast.success('Workout logged.')
          setOpen(false)
          reset()
        },
        onError: () => toast.error('Could not log workout.'),
      }
    )
  }

  return (
    <Dialog
      open={open}
      onOpenChange={(v) => {
        setOpen(v)
        if (!v) reset()
      }}
    >
      <DialogTrigger asChild>
        <Button size="sm">
          <Plus />
          Log Workout
        </Button>
      </DialogTrigger>
      <DialogContent className="max-h-[85vh] max-w-lg overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Log a workout</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <Label>Template (optional)</Label>
            <Select value={templateId} onValueChange={applyTemplate}>
              <SelectTrigger className="w-full">
                <SelectValue placeholder="No template — log freely" />
              </SelectTrigger>
              <SelectContent>
                {templates?.map((t) => (
                  <SelectItem key={t.id} value={t.id}>
                    {t.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2">
            <Label>Exercises</Label>
            {rows.map((row, i) => (
              <div key={i} className="flex items-end gap-2 rounded-md border p-2">
                <div className="flex-1 space-y-1">
                  <Select value={row.exerciseId} onValueChange={(v) => updateRow(i, { exerciseId: v })}>
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
                </div>
                <Input
                  className="w-16"
                  type="number"
                  min={0}
                  placeholder="Sets"
                  value={row.setsCompleted}
                  onChange={(e) => updateRow(i, { setsCompleted: e.target.value })}
                />
                <Input
                  className="w-16"
                  type="number"
                  min={0}
                  placeholder="Reps"
                  value={row.repsCompleted}
                  onChange={(e) => updateRow(i, { repsCompleted: e.target.value })}
                />
                <Input
                  className="w-20"
                  type="number"
                  min={0}
                  step="0.5"
                  placeholder="kg"
                  value={row.weightKg}
                  onChange={(e) => updateRow(i, { weightKg: e.target.value })}
                />
                <Button
                  type="button"
                  size="icon"
                  variant="ghost"
                  className="size-8 shrink-0"
                  onClick={() => removeRow(i)}
                  disabled={rows.length === 1}
                >
                  <Trash2 className="size-3.5" />
                </Button>
              </div>
            ))}
            <Button type="button" size="sm" variant="outline" onClick={() => setRows((prev) => [...prev, { ...emptyRow }])}>
              <Plus />
              Add Exercise
            </Button>
          </div>

          <DialogFooter>
            <Button type="submit" disabled={logWorkout.isPending}>
              {logWorkout.isPending && <Loader2 className="size-4 animate-spin" />}
              Log Workout
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
