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
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { useAddMealEntry, useFoodItemsList, type MealType } from '@/modules/nutrition/api/nutritionApi'

const MEAL_TYPES: MealType[] = ['Breakfast', 'Lunch', 'Dinner', 'Snack']

export function AddMealEntryDialog({ dietPlanId }: { dietPlanId: string }) {
  const [open, setOpen] = useState(false)
  const [foodItemId, setFoodItemId] = useState('')
  const [mealType, setMealType] = useState<MealType>('Breakfast')
  const [quantity, setQuantity] = useState('1')

  const { data: foodItems } = useFoodItemsList()
  const addMeal = useAddMealEntry(dietPlanId)

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!foodItemId) {
      toast.error('Select a food item.')
      return
    }

    addMeal.mutate(
      { foodItemId, mealType, quantity: Number(quantity) },
      {
        onSuccess: () => {
          toast.success('Meal entry added.')
          setOpen(false)
          setFoodItemId('')
          setQuantity('1')
        },
        onError: () => toast.error('Could not add meal entry.'),
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button size="sm" variant="outline">
          <Plus />
          Add Meal
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Add meal entry</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <Label>Food item</Label>
            <Select value={foodItemId} onValueChange={setFoodItemId}>
              <SelectTrigger className="w-full">
                <SelectValue placeholder="Select food item" />
              </SelectTrigger>
              <SelectContent>
                {foodItems?.map((f) => (
                  <SelectItem key={f.id} value={f.id}>
                    {f.name} ({f.servingSizeDescription})
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label>Meal</Label>
              <Select value={mealType} onValueChange={(v) => setMealType(v as MealType)}>
                <SelectTrigger className="w-full">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {MEAL_TYPES.map((m) => (
                    <SelectItem key={m} value={m}>
                      {m}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="quantity">Servings</Label>
              <Input
                id="quantity"
                type="number"
                min={0}
                step="0.5"
                required
                value={quantity}
                onChange={(e) => setQuantity(e.target.value)}
              />
            </div>
          </div>
          <DialogFooter>
            <Button type="submit" disabled={addMeal.isPending}>
              {addMeal.isPending && <Loader2 className="size-4 animate-spin" />}
              Add
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
