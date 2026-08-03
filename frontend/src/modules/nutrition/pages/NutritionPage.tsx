import { useState } from 'react'

import { Badge } from '@/components/ui/badge'
import { Card, CardContent } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { useMembersList } from '@/modules/members/api/membersApi'
import { AddMealEntryDialog } from '@/modules/nutrition/components/AddMealEntryDialog'
import { CreateDietPlanDialog } from '@/modules/nutrition/components/CreateDietPlanDialog'
import { CreateFoodItemDialog } from '@/modules/nutrition/components/CreateFoodItemDialog'
import { LogWaterDialog } from '@/modules/nutrition/components/LogWaterDialog'
import {
  useDietPlan,
  useFoodItemsList,
  useMemberDietPlans,
  useMemberWaterLogs,
  type DietPlanListItem,
} from '@/modules/nutrition/api/nutritionApi'

function DietPlanCard({
  planListItem,
  isSelected,
  onSelect,
}: {
  planListItem: DietPlanListItem
  isSelected: boolean
  onSelect: () => void
}) {
  const { data: plan } = useDietPlan(isSelected ? planListItem.id : undefined)

  return (
    <div>
      <button
        type="button"
        onClick={onSelect}
        className="flex w-full items-center justify-between rounded-md border p-3 text-left text-sm hover:bg-accent"
      >
        <span className="font-medium">{planListItem.name}</span>
      </button>
      {isSelected && plan && (
        <div className="space-y-2 rounded-b-md border border-t-0 p-3">
          <div className="flex items-center justify-between text-sm text-muted-foreground">
            <span>
              {new Date(plan.startDate).toLocaleDateString()}
              {plan.endDate && ` → ${new Date(plan.endDate).toLocaleDateString()}`}
              {plan.targetCalories && ` · Target ${plan.targetCalories} kcal`}
            </span>
            <AddMealEntryDialog dietPlanId={plan.id} />
          </div>
          {plan.mealEntries.length === 0 && <p className="text-sm text-muted-foreground">No meals logged yet.</p>}
          {plan.mealEntries.map((m) => (
            <div key={m.id} className="flex items-center justify-between text-sm">
              <span>
                {m.foodItemName} × {m.quantity}
              </span>
              <div className="flex items-center gap-2">
                <Badge variant="outline">{m.mealType}</Badge>
                {m.consumedAt && <span className="text-xs text-muted-foreground">{new Date(m.consumedAt).toLocaleString()}</span>}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}

function MemberNutrition() {
  const [searchTerm, setSearchTerm] = useState('')
  const [memberId, setMemberId] = useState('')
  const [memberName, setMemberName] = useState('')
  const [selectedPlanId, setSelectedPlanId] = useState('')

  const { data: members } = useMembersList({ searchTerm: searchTerm || undefined, status: 'Active', page: 1, pageSize: 6 })
  const { data: plans, isLoading: plansLoading } = useMemberDietPlans(memberId || undefined)
  const { data: waterLogs, isLoading: waterLoading } = useMemberWaterLogs(memberId || undefined)

  return (
    <div className="space-y-3">
      <Input
        placeholder="Search member to view or log their nutrition..."
        value={memberId ? memberName : searchTerm}
        onChange={(e) => {
          setMemberId('')
          setSelectedPlanId('')
          setSearchTerm(e.target.value)
        }}
      />
      {!memberId && searchTerm && (
        <div className="divide-y rounded-md border">
          {members?.items.length === 0 && <p className="p-3 text-sm text-muted-foreground">No members match.</p>}
          {members?.items.map((m) => (
            <button
              key={m.id}
              type="button"
              onClick={() => {
                setMemberId(m.id)
                setMemberName(m.fullName)
                setSearchTerm('')
              }}
              className="flex w-full items-center justify-between px-3 py-2 text-left text-sm hover:bg-accent"
            >
              {m.fullName}
              <span className="text-xs text-muted-foreground">{m.memberCode}</span>
            </button>
          ))}
        </div>
      )}

      {memberId && (
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <p className="text-sm font-medium">{memberName}'s diet plans</p>
              <CreateDietPlanDialog memberId={memberId} />
            </div>
            {plansLoading && <Skeleton className="h-16 w-full" />}
            {plans?.length === 0 && <p className="text-sm text-muted-foreground">No diet plans yet.</p>}
            <div className="space-y-2">
              {plans?.map((p) => (
                <DietPlanCard
                  key={p.id}
                  planListItem={p}
                  isSelected={selectedPlanId === p.id}
                  onSelect={() => setSelectedPlanId((prev) => (prev === p.id ? '' : p.id))}
                />
              ))}
            </div>
          </div>

          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <p className="text-sm font-medium">{memberName}'s water intake</p>
              <LogWaterDialog memberId={memberId} />
            </div>
            {waterLoading && <Skeleton className="h-16 w-full" />}
            {waterLogs?.length === 0 && <p className="text-sm text-muted-foreground">No water logged yet.</p>}
            <div className="space-y-1">
              {waterLogs?.map((w) => (
                <div key={w.id} className="flex items-center justify-between rounded-md border px-3 py-2 text-sm">
                  <span>{w.amountMl} ml</span>
                  <span className="text-xs text-muted-foreground">{new Date(w.loggedAt).toLocaleString()}</span>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}
    </div>
  )
}

export default function NutritionPage() {
  const { data: foodItems, isLoading } = useFoodItemsList()

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Nutrition</h1>
        <p className="text-sm text-muted-foreground">Food library, diet plans, and hydration tracking.</p>
      </div>

      <Tabs defaultValue="food">
        <TabsList>
          <TabsTrigger value="food">Food Library</TabsTrigger>
          <TabsTrigger value="member">Member Nutrition</TabsTrigger>
        </TabsList>

        <TabsContent value="food" className="space-y-3">
          <div className="flex justify-end">
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
        </TabsContent>

        <TabsContent value="member">
          <MemberNutrition />
        </TabsContent>
      </Tabs>
    </div>
  )
}
