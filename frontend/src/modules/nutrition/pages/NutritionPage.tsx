import { Badge } from '@/components/ui/badge'
import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useFoodItemsList } from '@/modules/nutrition/api/nutritionApi'
import { CreateFoodItemDialog } from '@/modules/nutrition/components/CreateFoodItemDialog'

export default function NutritionPage() {
  const { data: foodItems, isLoading } = useFoodItemsList()

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Nutrition</h1>
          <p className="text-sm text-muted-foreground">Food library for building member diet plans.</p>
        </div>
        <CreateFoodItemDialog />
      </div>

      {isLoading ? (
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 6 }).map((_, i) => (
            <Skeleton key={i} className="h-28 w-full" />
          ))}
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {foodItems?.map((food) => (
            <Card key={food.id}>
              <CardContent className="space-y-2 p-3">
                <div className="flex items-center justify-between">
                  <p className="font-medium">{food.name}</p>
                  <span className="text-sm text-muted-foreground">{food.servingSizeDescription}</span>
                </div>
                <div className="flex flex-wrap gap-1">
                  <Badge variant="default">{food.caloriesPerServing} kcal</Badge>
                  <Badge variant="outline">P {food.proteinG}g</Badge>
                  <Badge variant="outline">C {food.carbsG}g</Badge>
                  <Badge variant="outline">F {food.fatG}g</Badge>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  )
}
