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
import { Textarea } from '@/components/ui/textarea'
import { useCreateClassType } from '@/modules/classes/api/classesApi'

const PRESET_COLORS = ['#2563eb', '#16a34a', '#dc2626', '#7c3aed', '#db2777', '#ea580c', '#0891b2', '#65a30d']

export function CreateClassTypeDialog() {
  const [open, setOpen] = useState(false)
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [duration, setDuration] = useState(45)
  const [capacity, setCapacity] = useState(20)
  const [colorHex, setColorHex] = useState(PRESET_COLORS[0])

  const createType = useCreateClassType()

  const reset = () => {
    setName('')
    setDescription('')
    setDuration(45)
    setCapacity(20)
    setColorHex(PRESET_COLORS[0])
  }

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    createType.mutate(
      {
        name,
        description: description || undefined,
        defaultDurationMinutes: duration,
        defaultCapacity: capacity,
        colorHex,
      },
      {
        onSuccess: () => {
          toast.success('Class type created.')
          setOpen(false)
          reset()
        },
        onError: () => toast.error('Could not create class type.'),
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button size="sm" variant="outline">
          <Plus />
          New Class Type
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>New class type</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="classTypeName">Name</Label>
            <Input id="classTypeName" required value={name} onChange={(e) => setName(e.target.value)} placeholder="e.g. Spin" />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="classTypeDescription">Description (optional)</Label>
            <Textarea id="classTypeDescription" value={description} onChange={(e) => setDescription(e.target.value)} />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="classTypeDuration">Default duration (min)</Label>
              <Input
                id="classTypeDuration"
                type="number"
                min={5}
                max={480}
                required
                value={duration}
                onChange={(e) => setDuration(Number(e.target.value))}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="classTypeCapacity">Default capacity</Label>
              <Input
                id="classTypeCapacity"
                type="number"
                min={1}
                max={1000}
                required
                value={capacity}
                onChange={(e) => setCapacity(Number(e.target.value))}
              />
            </div>
          </div>
          <div className="space-y-1.5">
            <Label>Colour</Label>
            <div className="flex flex-wrap gap-2">
              {PRESET_COLORS.map((color) => (
                <button
                  key={color}
                  type="button"
                  aria-label={`Select colour ${color}`}
                  onClick={() => setColorHex(color)}
                  className={`size-7 rounded-full border-2 ${colorHex === color ? 'border-foreground' : 'border-transparent'}`}
                  style={{ backgroundColor: color }}
                />
              ))}
            </div>
          </div>
          <DialogFooter>
            <Button type="submit" disabled={createType.isPending}>
              {createType.isPending && <Loader2 className="size-4 animate-spin" />}
              Create
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
