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
import { useCreateFoodItem } from '@/modules/nutrition/api/nutritionApi'

export function CreateFoodItemDialog() {
  const [open, setOpen] = useState(false)
  const [name, setName] = useState('')
  const [servingSizeDescription, setServingSizeDescription] = useState('')
  const [caloriesPerServing, setCaloriesPerServing] = useState(0)
  const [proteinG, setProteinG] = useState(0)
  const [carbsG, setCarbsG] = useState(0)
  const [fatG, setFatG] = useState(0)

  const createFoodItem = useCreateFoodItem()

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    createFoodItem.mutate(
      { name, caloriesPerServing, proteinG, carbsG, fatG, servingSizeDescription },
      {
        onSuccess: () => {
          toast.success('Food item added.')
          setOpen(false)
          setName('')
          setServingSizeDescription('')
        },
        onError: () => toast.error('Could not add food item.'),
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button>
          <Plus />
          Add Food Item
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Add food item</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="name">Name</Label>
              <Input id="name" required value={name} onChange={(e) => setName(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="serving">Serving size</Label>
              <Input id="serving" required placeholder="100g" value={servingSizeDescription} onChange={(e) => setServingSizeDescription(e.target.value)} />
            </div>
          </div>
          <div className="grid grid-cols-4 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="calories">Calories</Label>
              <Input id="calories" type="number" min={0} value={caloriesPerServing} onChange={(e) => setCaloriesPerServing(Number(e.target.value))} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="protein">Protein (g)</Label>
              <Input id="protein" type="number" min={0} step="0.1" value={proteinG} onChange={(e) => setProteinG(Number(e.target.value))} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="carbs">Carbs (g)</Label>
              <Input id="carbs" type="number" min={0} step="0.1" value={carbsG} onChange={(e) => setCarbsG(Number(e.target.value))} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="fat">Fat (g)</Label>
              <Input id="fat" type="number" min={0} step="0.1" value={fatG} onChange={(e) => setFatG(Number(e.target.value))} />
            </div>
          </div>
          <DialogFooter>
            <Button type="submit" disabled={createFoodItem.isPending}>
              {createFoodItem.isPending && <Loader2 className="size-4 animate-spin" />}
              Add
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
